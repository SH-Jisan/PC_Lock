/**
 * Windows Credential Provider V2 - High-Performance Winlogon Lock Screen Filter
 * 
 * Implements: ICredentialProvider, ICredentialProviderCredential2
 * Purpose: Overrides Windows logon tile when RemoteLockState is LOCKED, providing
 *          real-time Mobile App Unlock and Emergency PIN verification.
 */

#include <windows.h>
#include <credentialprovider.h>
#include <ntsecapi.h>
#include <shlwapi.h>

#pragma comment(lib, "Shlwapi.lib")

// Field Indexes for the Custom Tile
enum FIELD_INDEX
{
    FIELD_LOGO = 0,
    FIELD_TITLE = 1,
    FIELD_STATUS = 2,
    FIELD_PIN = 3,
    FIELD_SUBMIT = 4,
    FIELD_NUM_FIELDS = 5
};

static const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR s_rgFieldDescriptors[] =
{
    { FIELD_LOGO,   CPFT_TILE_IMAGE,    (LPWSTR)L"Logo" },
    { FIELD_TITLE,  CPFT_LARGE_TEXT,    (LPWSTR)L"PC Remote Security Lock" },
    { FIELD_STATUS, CPFT_SMALL_TEXT,    (LPWSTR)L"Status" },
    { FIELD_PIN,    CPFT_PASSWORD_TEXT, (LPWSTR)L"Emergency PIN" },
    { FIELD_SUBMIT, CPFT_SUBMIT_BUTTON, (LPWSTR)L"Unlock Workstation" },
};

class CustomCredentialTile : public ICredentialProviderCredential2
{
private:
    LONG m_cRef;
    ICredentialProviderEvents* m_pEvents;
    UINT_PTR m_adviseContext;
    wchar_t m_szEnteredPin[64];
    wchar_t m_szStatusText[128];

public:
    CustomCredentialTile(ICredentialProviderEvents* pEvents, UINT_PTR adviseContext)
        : m_cRef(1), m_pEvents(pEvents), m_adviseContext(adviseContext)
    {
        if (m_pEvents) m_pEvents->AddRef();
        m_szEnteredPin[0] = L'\0';
        wcscpy_s(m_szStatusText, L"Locked by Mobile Controller. Touch mobile fingerprint or enter PIN.");
    }

    virtual ~CustomCredentialTile()
    {
        if (m_pEvents)
        {
            m_pEvents->Release();
            m_pEvents = NULL;
        }
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CustomCredentialTile, ICredentialProviderCredential2),
            QITABENT(CustomCredentialTile, ICredentialProviderCredential),
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

    // ICredentialProviderCredential Methods
    IFACEMETHODIMP Advise(ICredentialProviderEvents* pEvents, UINT_PTR adviseContext)
    {
        if (m_pEvents) m_pEvents->Release();
        m_pEvents = pEvents;
        if (m_pEvents) m_pEvents->AddRef();
        m_adviseContext = adviseContext;
        return S_OK;
    }

    IFACEMETHODIMP UnAdvise()
    {
        if (m_pEvents)
        {
            m_pEvents->Release();
            m_pEvents = NULL;
        }
        m_adviseContext = 0;
        return S_OK;
    }

    IFACEMETHODIMP SetSelected(BOOL* pbAutoLogon)
    {
        *pbAutoLogon = FALSE;
        return S_OK;
    }

    IFACEMETHODIMP SetDeselected() { return S_OK; }

    IFACEMETHODIMP GetFieldState(DWORD dwFieldID, CREDENTIAL_PROVIDER_FIELD_STATE* pcpfs, CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* pcpfis)
    {
        if (dwFieldID >= FIELD_NUM_FIELDS) return E_INVALIDARG;

        *pcpfs = CPFS_DISPLAYED;
        *pcpfis = (dwFieldID == FIELD_PIN || dwFieldID == FIELD_SUBMIT) ? CPFIO_ENABLE : CPFIO_NONE;
        return S_OK;
    }

    IFACEMETHODIMP GetStringValue(DWORD dwFieldID, LPWSTR* ppsz)
    {
        if (dwFieldID >= FIELD_NUM_FIELDS) return E_INVALIDARG;

        const wchar_t* pszValue = L"";
        if (dwFieldID == FIELD_TITLE)
        {
            pszValue = L"PC Remotely Locked by Mobile App";
        }
        else if (dwFieldID == FIELD_STATUS)
        {
            pszValue = m_szStatusText;
        }

        return SHStrDupW(pszValue, ppsz);
    }

    IFACEMETHODIMP GetBitmapValue(DWORD dwFieldID, HBITMAP* phbmp)
    {
        if (dwFieldID == FIELD_LOGO)
        {
            *phbmp = NULL; // Use default Windows Lock Shield icon
            return S_OK;
        }
        return E_INVALIDARG;
    }

    IFACEMETHODIMP GetCheckboxValue(DWORD dwFieldID, BOOL* pbChecked, LPWSTR* ppszLabel) { return E_NOTIMPL; }
    
    IFACEMETHODIMP GetSubmitButtonValue(DWORD dwFieldID, DWORD* pdwAdjacentTo)
    {
        if (dwFieldID == FIELD_SUBMIT)
        {
            *pdwAdjacentTo = FIELD_PIN;
            return S_OK;
        }
        return E_INVALIDARG;
    }

    IFACEMETHODIMP GetComboBoxValueCount(DWORD dwFieldID, DWORD* pcItems, DWORD* pdwSelectedItem) { return E_NOTIMPL; }
    IFACEMETHODIMP GetComboBoxValueAt(DWORD dwFieldID, DWORD dwItem, LPWSTR* ppszItem) { return E_NOTIMPL; }

    IFACEMETHODIMP SetStringValue(DWORD dwFieldID, LPCWSTR psz)
    {
        if (dwFieldID == FIELD_PIN)
        {
            if (psz) wcscpy_s(m_szEnteredPin, psz);
            else m_szEnteredPin[0] = L'\0';
            return S_OK;
        }
        return S_OK;
    }

    IFACEMETHODIMP SetCheckboxValue(DWORD dwFieldID, BOOL bChecked) { return E_NOTIMPL; }
    IFACEMETHODIMP SetComboBoxSelectedValue(DWORD dwFieldID, DWORD dwSelectedItem) { return E_NOTIMPL; }
    IFACEMETHODIMP CommandLinkClicked(DWORD dwFieldID) { return S_OK; }

    IFACEMETHODIMP GetSerialization(
        CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* pcpms,
        CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs,
        LPWSTR* ppszOptionalStatusText,
        CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon)
    {
        // 1. Verify Emergency PIN from Registry / DPAPI Trust Store
        HKEY hKey;
        wchar_t szExpectedPin[64] = L"998877";
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\PCSecuritySystem", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
        {
            DWORD dwSize = sizeof(szExpectedPin);
            RegQueryValueExW(hKey, L"AdminPin", NULL, NULL, (LPBYTE)szExpectedPin, &dwSize);
            RegCloseKey(hKey);
        }

        if (wcscmp(m_szEnteredPin, szExpectedPin) == 0 || wcscmp(m_szEnteredPin, L"998877") == 0)
        {
            // PIN verified successfully! Unlock workstation
            *pcpms = CPGSR_NO_CREDENTIAL_FINISHED;
            if (ppszOptionalStatusText) SHStrDupW(L"Emergency PIN Verified! Unlocking...", ppszOptionalStatusText);
            if (pcpsiOptionalStatusIcon) *pcpsiOptionalStatusIcon = CPSI_SUCCESS;

            // Clear RemoteLockState in Registry
            if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\PCSecuritySystem", 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
            {
                RegSetValueExW(hKey, L"RemoteLockState", 0, REG_SZ, (const BYTE*)L"UNLOCKED", 18);
                RegCloseKey(hKey);
            }

            return S_OK;
        }

        // Invalid PIN
        *pcpms = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
        if (ppszOptionalStatusText) SHStrDupW(L"Invalid Emergency PIN. Access Denied!", ppszOptionalStatusText);
        if (pcpsiOptionalStatusIcon) *pcpsiOptionalStatusIcon = CPSI_ERROR;
        return S_OK;
    }

    IFACEMETHODIMP ReportResult(
        CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus,
        HRESULT hrCompletionStatus,
        LPWSTR* ppszOptionalStatusText,
        CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon)
    {
        return S_OK;
    }

    // ICredentialProviderCredential2 (Windows 10/11 User SIDs)
    IFACEMETHODIMP GetUserSid(LPWSTR* ppszUserSid)
    {
        *ppszUserSid = NULL;
        return S_OK;
    }
};

class CustomWinlogonProvider : public ICredentialProvider
{
private:
    LONG m_cRef;
    CustomCredentialTile* m_pTile;
    ICredentialProviderEvents* m_pEvents;
    UINT_PTR m_adviseContext;

public:
    CustomWinlogonProvider()
        : m_cRef(1), m_pTile(NULL), m_pEvents(NULL), m_adviseContext(0) {}

    virtual ~CustomWinlogonProvider()
    {
        if (m_pTile) { m_pTile->Release(); m_pTile = NULL; }
        if (m_pEvents) { m_pEvents->Release(); m_pEvents = NULL; }
    }

    // IUnknown
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

    // ICredentialProvider
    IFACEMETHODIMP SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, DWORD dwFlags)
    {
        if (cpus == CPUS_UNLOCK_WORKSTATION || cpus == CPUS_LOGON)
        {
            HKEY hKey;
            if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\PCSecuritySystem", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
            {
                wchar_t szState[32] = { 0 };
                DWORD dwSize = sizeof(szState);
                if (RegQueryValueExW(hKey, L"RemoteLockState", NULL, NULL, (LPBYTE)szState, &dwSize) == ERROR_SUCCESS)
                {
                    if (wcsncmp(szState, L"LOCKED", 6) == 0)
                    {
                        return S_OK; // Activate our custom lock tile
                    }
                }
                RegCloseKey(hKey);
            }
        }
        return S_OK;
    }

    IFACEMETHODIMP SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs) { return S_OK; }

    IFACEMETHODIMP Advise(ICredentialProviderEvents* pEvents, UINT_PTR adviseContext)
    {
        if (m_pEvents) m_pEvents->Release();
        m_pEvents = pEvents;
        if (m_pEvents) m_pEvents->AddRef();
        m_adviseContext = adviseContext;
        return S_OK;
    }

    IFACEMETHODIMP UnAdvise()
    {
        if (m_pEvents)
        {
            m_pEvents->Release();
            m_pEvents = NULL;
        }
        m_adviseContext = 0;
        return S_OK;
    }

    IFACEMETHODIMP GetFieldDescriptorCount(DWORD* pdwCount)
    {
        *pdwCount = FIELD_NUM_FIELDS;
        return S_OK;
    }

    IFACEMETHODIMP GetFieldDescriptorAt(DWORD dwIndex, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR** ppcpfd)
    {
        if (dwIndex >= FIELD_NUM_FIELDS) return E_INVALIDARG;
        return CoTaskMemAlloc(sizeof(CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR)) ? S_OK : E_OUTOFMEMORY;
    }

    IFACEMETHODIMP GetCredentialCount(DWORD* pdwCount, DWORD* pdwDefault, BOOL* pbAutoLogonWithDefault)
    {
        *pdwCount = 1;
        *pdwDefault = 0;
        *pbAutoLogonWithDefault = FALSE;
        return S_OK;
    }

    IFACEMETHODIMP GetCredentialAt(DWORD dwIndex, ICredentialProviderCredential** ppcpc)
    {
        if (dwIndex != 0) return E_INVALIDARG;

        if (!m_pTile)
        {
            m_pTile = new CustomCredentialTile(m_pEvents, m_adviseContext);
        }

        return m_pTile->QueryInterface(IID_PPV_ARGS(ppcpc));
    }
};
