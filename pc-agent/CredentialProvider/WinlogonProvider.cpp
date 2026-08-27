/*
 * Windows Credential Provider V2 - Winlogon Lock Screen Filter Reference
 * 
 * Target: ICredentialProviderSetUserArray, ICredentialProviderCredential2
 * Purpose: Overrides Windows logon tile when RemoteLockState registry key is LOCKED.
 */

#include <windows.h>
#include <credentialprovider.h>
#include <ntsecapi.h>

class CustomWinlogonProvider : public ICredentialProvider
{
private:
    LONG m_cRef;

public:
    CustomWinlogonProvider() : m_cRef(1) {}

    // IUnknown methods
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CustomWinlogonProvider, ICredentialProvider),
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return InterlockedIncrement(&m_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        LONG cRef = InterlockedDecrement(&m_cRef);
        if (!cRef) delete this;
        return cRef;
    }

    // ICredentialProvider methods
    IFACEMETHODIMP SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, DWORD dwFlags)
    {
        if (cpus == CPUS_UNLOCK_WORKSTATION || cpus == CPUS_LOGON)
        {
            // Check Registry RemoteLockState
            HKEY hKey;
            if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\PCSecuritySystem", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
            {
                wchar_t szState[32] = { 0 };
                DWORD dwSize = sizeof(szState);
                if (RegQueryValueExW(hKey, L"RemoteLockState", NULL, NULL, (LPBYTE)szState, &dwSize) == ERROR_SUCCESS)
                {
                    if (wcsncmp(szState, L"LOCKED", 6) == 0)
                    {
                        // PC is in Remote Locked state: Enforce custom mobile authentication tile
                        return S_OK;
                    }
                }
                RegCloseKey(hKey);
            }
        }
        return S_OK;
    }

    IFACEMETHODIMP SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs) { return S_OK; }
    IFACEMETHODIMP GetCredentialCount(DWORD* pdwCount, DWORD* pdwDefault, BOOL* pbAutoLogonWithDefault)
    {
        *pdwCount = 1;
        *pdwDefault = 0;
        *pbAutoLogonWithDefault = FALSE;
        return S_OK;
    }

    IFACEMETHODIMP GetCredentialAt(DWORD dwIndex, ICredentialProviderCredential** ppcpc)
    {
        return E_NOTIMPL; // Implement ICredentialProviderCredential2 tile renderer
    }
};
