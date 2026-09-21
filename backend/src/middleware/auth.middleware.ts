import { Request, Response, NextFunction } from 'express';
import jwt from 'jsonwebtoken';

const JWT_SECRET = process.env.JWT_SECRET || 'pc_security_master_jwt_secret_2026';
const ADMIN_MASTER_KEY = process.env.ADMIN_MASTER_KEY || 'pc_security_master_admin_2026';

export function requireAdminAuth(req: Request, res: Response, next: NextFunction) {
  const authHeader = req.headers['authorization'];
  const adminKeyHeader = req.headers['x-admin-key'];

  if (adminKeyHeader === ADMIN_MASTER_KEY) {
    return next();
  }

  if (authHeader && authHeader.startsWith('Bearer ')) {
    const token = authHeader.substring(7);
    try {
      const decoded: any = jwt.verify(token, JWT_SECRET);
      if (decoded && decoded.userId) {
        return next();
      }
    } catch {
      return res.status(401).json({ status: 'ERROR', message: 'Invalid or expired administrative token' });
    }
  }

  return res.status(401).json({ status: 'ERROR', message: 'Unauthorized: Admin privileges required' });
}
