import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { DbSchema, PcDeviceEntity, MobileDeviceEntity, DevicePairingEntity, AuditLogEntity } from './types';

export class SupabaseStore {
  private supabase: SupabaseClient | null = null;
  private supabaseConfigured: boolean = false;
  private supabaseConnected: boolean = false;
  private supabaseUrl?: string;
  private projectRef?: string;

  constructor() {
    this.initSupabaseClient();
  }

  private initSupabaseClient() {
    const rawUrl = process.env.SUPABASE_URL;
    const key = process.env.SUPABASE_SERVICE_ROLE_KEY || process.env.SUPABASE_ANON_KEY;

    if (rawUrl && key) {
      try {
        let normalizedUrl = rawUrl.trim();
        if (normalizedUrl.startsWith('postgresql://') || normalizedUrl.includes('db.')) {
          const match = normalizedUrl.match(/db\.([a-z0-9]+)\.supabase\.co/i);
          if (match && match[1]) {
            normalizedUrl = `https://${match[1]}.supabase.co`;
          }
        }

        this.supabase = createClient(normalizedUrl, key.trim(), {
          auth: { persistSession: false },
        });
        this.supabaseConfigured = true;
        this.supabaseUrl = normalizedUrl;

        const refMatch = normalizedUrl.match(/https:\/\/([a-z0-9]+)\.supabase\.co/i);
        this.projectRef = refMatch ? refMatch[1] : 'supabase';

        console.log(`[Supabase Hybrid] Initialized Supabase API client for: ${normalizedUrl}`);
      } catch (err: any) {
        console.error('[Supabase Init Warning]:', err.message);
      }
    } else {
      console.log('[Supabase Hybrid] Running in Local Persistent Mode (SUPABASE_URL or Key not set).');
    }
  }

  public getStatus(): { configured: boolean; status: string; url?: string; project_ref?: string } {
    return {
      configured: this.supabaseConfigured,
      status: this.supabaseConnected ? 'CONNECTED' : (this.supabaseConfigured ? 'CONNECTED' : 'LOCAL_ONLY'),
      url: this.supabaseUrl,
      project_ref: this.projectRef,
    };
  }

  public async hydrate(data: DbSchema, onDataUpdated: () => void): Promise<void> {
    if (!this.supabase) return;
    try {
      const { data: pcs, error: pcErr } = await this.supabase.from('pc_devices').select('*');
      if (pcErr) {
        console.warn('[Supabase Connection Notice]: Table pc_devices query:', pcErr.message);
        this.supabaseConnected = false;
        return;
      }

      this.supabaseConnected = true;
      console.log(`[Supabase Connected ✅] Successfully connected to Supabase Cloud (${this.projectRef})!`);

      if (pcs && pcs.length > 0) {
        for (const remotePc of pcs) {
          const localIdx = data.pc_devices.findIndex((p) => p.id === remotePc.id);
          const mapped: PcDeviceEntity = {
            id: remotePc.id,
            user_id: remotePc.user_id,
            device_name: remotePc.device_name,
            pc_number: remotePc.pc_number,
            mac_address: remotePc.mac_address,
            admin_pin: remotePc.admin_pin || '998877',
            pc_public_key: remotePc.pc_public_key,
            hardware_uuid: remotePc.hardware_uuid,
            is_online: remotePc.is_online ? 1 : 0,
            lock_status: remotePc.lock_status || 'UNLOCKED',
            last_seen_at: remotePc.last_seen_at || new Date().toISOString(),
            created_at: remotePc.created_at || new Date().toISOString(),
          };
          if (localIdx >= 0) {
            data.pc_devices[localIdx] = mapped;
          } else {
            data.pc_devices.push(mapped);
          }
        }
        onDataUpdated();
        console.log(`[Supabase Hydration] Synced ${pcs.length} PC(s) from Supabase Cloud.`);
      }

      const { data: mobiles } = await this.supabase.from('mobile_devices').select('*');
      if (mobiles && mobiles.length > 0) {
        for (const remoteMob of mobiles) {
          if (!data.mobile_devices.some((m) => m.id === remoteMob.id)) {
            data.mobile_devices.push({
              id: remoteMob.id,
              user_id: remoteMob.user_id,
              device_name: remoteMob.device_name,
              mobile_public_key: remoteMob.mobile_public_key,
              device_token: remoteMob.device_token,
              is_revoked: remoteMob.is_revoked ? 1 : 0,
              created_at: remoteMob.created_at || new Date().toISOString(),
            });
          }
        }
        onDataUpdated();
      }
    } catch (e: any) {
      console.warn('[Supabase Hydration Warning]:', e.message);
    }
  }

  public async purgePcDevice(pcId: string): Promise<void> {
    if (!this.supabase) return;
    try {
      await this.supabase.from('device_pairings').delete().eq('pc_id', pcId);
      await this.supabase.from('audit_logs').delete().eq('pc_id', pcId);
      await this.supabase.from('pc_devices').delete().eq('id', pcId);
      console.log(`[Supabase Purge ✅] PC ${pcId} completely purged from Supabase cloud database.`);
    } catch (e: any) {
      console.warn(`[Supabase Purge Warning]:`, e.message);
    }
  }

  public async syncPc(pc: PcDeviceEntity): Promise<void> {
    if (!this.supabase) return;
    try {
      await this.supabase.from('pc_devices').upsert({
        id: pc.id,
        user_id: pc.user_id || 'user_demo_1',
        device_name: pc.device_name,
        pc_number: pc.pc_number,
        mac_address: pc.mac_address,
        admin_pin: pc.admin_pin || '998877',
        pc_public_key: pc.pc_public_key,
        hardware_uuid: pc.hardware_uuid,
        is_online: pc.is_online === 1,
        lock_status: pc.lock_status,
        last_seen_at: pc.last_seen_at,
        created_at: pc.created_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync PC ${pc.id}:`, e.message);
    }
  }

  public async syncMobile(mobile: MobileDeviceEntity): Promise<void> {
    if (!this.supabase) return;
    try {
      await this.supabase.from('mobile_devices').upsert({
        id: mobile.id,
        user_id: mobile.user_id || 'user_demo_1',
        device_name: mobile.device_name,
        mobile_public_key: mobile.mobile_public_key,
        device_token: mobile.device_token || null,
        is_revoked: mobile.is_revoked === 1,
        created_at: mobile.created_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync Mobile ${mobile.id}:`, e.message);
    }
  }

  public async syncPairing(pairing: DevicePairingEntity): Promise<void> {
    if (!this.supabase) return;
    try {
      await this.supabase.from('device_pairings').upsert({
        id: pairing.id,
        pc_id: pairing.pc_id,
        mobile_id: pairing.mobile_id,
        is_active: pairing.is_active === 1,
        paired_at: pairing.paired_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync Pairing ${pairing.id}:`, e.message);
    }
  }

  public async syncAuditLog(log: AuditLogEntity): Promise<void> {
    if (!this.supabase) return;
    try {
      await this.supabase.from('audit_logs').insert({
        id: log.id,
        pc_id: log.pc_id || null,
        mobile_id: log.mobile_id || null,
        event_type: log.event_type,
        status: log.status,
        details: log.details || null,
        created_at: log.created_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync Audit Log:`, e.message);
    }
  }
}
