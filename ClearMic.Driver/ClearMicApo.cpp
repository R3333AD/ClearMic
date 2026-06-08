#include <initguid.h>
#include "ClearMicApo.h"

static LONG g_cRefDll = 0;
static HINSTANCE g_hModule = nullptr;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

static HRESULT CreateAPORegProperties(APO_REG_PROPERTIES** ppProperties)
{
    auto p = (APO_REG_PROPERTIES*)CoTaskMemAlloc(sizeof(APO_REG_PROPERTIES));
    if (!p) return E_OUTOFMEMORY;
    ZeroMemory(p, sizeof(APO_REG_PROPERTIES));
    p->clsid = CLSID_ClearMicApo;
    p->Flags = APO_FLAG_DEFAULT;
    wcscpy_s(p->szFriendlyName, 256, L"ClearMic Noise Reduction");
    wcscpy_s(p->szCopyrightInfo, 256, L"ClearMic Contributors");
    p->u32MajorVersion = 1;
    p->u32MinorVersion = 0;
    p->u32MinInputConnections = 1;
    p->u32MaxInputConnections = 1;
    p->u32MinOutputConnections = 1;
    p->u32MaxOutputConnections = 1;
    p->u32MaxInstances = 0;
    *ppProperties = p;
    return S_OK;
}

static bool IsFormatSupported(const WAVEFORMATEX* wfx)
{
    if (!wfx) return false;
    if (wfx->wFormatTag == WAVE_FORMAT_EXTENSIBLE)
    {
        auto* wfxe = (WAVEFORMATEXTENSIBLE*)wfx;
        if (wfxe->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT)
            return wfx->nChannels == 1 && wfx->nSamplesPerSec == 48000 && wfx->wBitsPerSample == 32;
        if (wfxe->SubFormat == KSDATAFORMAT_SUBTYPE_PCM)
            return wfx->nChannels == 1 && wfx->nSamplesPerSec == 48000 && wfx->wBitsPerSample == 16;
        return false;
    }
    return (wfx->wFormatTag == WAVE_FORMAT_IEEE_FLOAT || wfx->wFormatTag == WAVE_FORMAT_PCM)
        && wfx->nChannels == 1
        && wfx->nSamplesPerSec == 48000
        && (wfx->wBitsPerSample == 32 || wfx->wBitsPerSample == 16);
}

// ---------------------------------------------------------------------------
// CClearMicApo
// ---------------------------------------------------------------------------

CClearMicApo::CClearMicApo()
    : _refCount(1)
    , _isInitialized(false)
    , _isLocked(false)
    , _pipeHandle(INVALID_HANDLE_VALUE)
{
    InterlockedIncrement(&g_cRefDll);
}

CClearMicApo::~CClearMicApo()
{
    ClosePipe();
    InterlockedDecrement(&g_cRefDll);
}

STDMETHODIMP CClearMicApo::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (riid == __uuidof(IUnknown) ||
        riid == __uuidof(IAudioProcessingObject))
        *ppv = static_cast<IAudioProcessingObject*>(this);
    else if (riid == __uuidof(IAudioProcessingObjectRT))
        *ppv = static_cast<IAudioProcessingObjectRT*>(this);
    else if (riid == __uuidof(IAudioProcessingObjectConfiguration))
        *ppv = static_cast<IAudioProcessingObjectConfiguration*>(this);
    else if (riid == __uuidof(IAudioSystemEffects))
        *ppv = static_cast<IAudioSystemEffects*>(this);
    else
        return E_NOINTERFACE;

    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) CClearMicApo::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

STDMETHODIMP_(ULONG) CClearMicApo::Release()
{
    ULONG ref = InterlockedDecrement(&_refCount);
    if (ref == 0) delete this;
    return ref;
}

STDMETHODIMP CClearMicApo::GetRegistrationProperties(APO_REG_PROPERTIES** ppProperties)
{
    if (!ppProperties) return E_POINTER;
    return CreateAPORegProperties(ppProperties);
}

STDMETHODIMP CClearMicApo::Initialize(UINT32, BYTE*)
{
    _isInitialized = true;
    return S_OK;
}

STDMETHODIMP CClearMicApo::Reset()
{
    ClosePipe();
    return S_OK;
}

STDMETHODIMP CClearMicApo::IsInputFormatSupported(
    IAudioMediaType* pOppositeFormat,
    IAudioMediaType* pRequestedInputFormat,
    IAudioMediaType** ppSupportedInputFormat)
{
    if (ppSupportedInputFormat) *ppSupportedInputFormat = nullptr;
    if (!pRequestedInputFormat) return E_POINTER;

    auto* pwfx = pRequestedInputFormat->GetAudioFormat();
    if (!pwfx) return APOERR_FORMAT_NOT_SUPPORTED;

    if (!::IsFormatSupported(pwfx))
        return APOERR_FORMAT_NOT_SUPPORTED;

    if (ppSupportedInputFormat)
    {
        *ppSupportedInputFormat = pRequestedInputFormat;
        (*ppSupportedInputFormat)->AddRef();
    }
    return S_OK;
}

STDMETHODIMP CClearMicApo::IsOutputFormatSupported(
    IAudioMediaType* pOppositeFormat,
    IAudioMediaType* pRequestedOutputFormat,
    IAudioMediaType** ppSupportedOutputFormat)
{
    return IsInputFormatSupported(pOppositeFormat, pRequestedOutputFormat, ppSupportedOutputFormat);
}

STDMETHODIMP CClearMicApo::GetInputChannelCount(UINT32* pu32ChannelCount)
{
    if (!pu32ChannelCount) return E_POINTER;
    *pu32ChannelCount = 1;
    return S_OK;
}

STDMETHODIMP CClearMicApo::GetLatency(HNSTIME* pTime)
{
    if (!pTime) return E_POINTER;
    *pTime = 0;
    return S_OK;
}

void STDMETHODCALLTYPE CClearMicApo::APOProcess(
    UINT32 u32NumInputConnections,
    APO_CONNECTION_PROPERTY** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_PROPERTY** ppOutputConnections)
{
    if (u32NumInputConnections < 1 || u32NumOutputConnections < 1)
        return;

    auto* pInput = ppInputConnections[0];
    auto* pOutput = ppOutputConnections[0];
    auto* pInBuf = reinterpret_cast<float*>(static_cast<UINT_PTR>(pInput->pBuffer));
    auto* pOutBuf = reinterpret_cast<float*>(static_cast<UINT_PTR>(pOutput->pBuffer));

    if (!pInBuf || !pOutBuf) return;

    UINT32 frameCount = pInput->u32ValidFrameCount;

    if (_pipeHandle == INVALID_HANDLE_VALUE)
    {
        if (FAILED(OpenPipe()))
            goto passthrough;
    }

    for (UINT32 offset = 0; offset < frameCount; offset += FRAME_SIZE)
    {
        UINT32 chunk = min(FRAME_SIZE, frameCount - offset);
        short inBuf[FRAME_SIZE];
        short outBuf[FRAME_SIZE];

        for (UINT32 i = 0; i < chunk; i++)
            inBuf[i] = (short)(pInBuf[offset + i] * 32767.0f);
        for (UINT32 i = chunk; i < FRAME_SIZE; i++)
            inBuf[i] = 0;

        BYTE wire[WIRE_SIZE];
        *(UINT32*)&wire[0] = offset / FRAME_SIZE;
        *(UINT32*)&wire[4] = 4;
        memcpy(&wire[8], inBuf, FRAME_SIZE * sizeof(short));

        DWORD written = 0;
        if (!WriteFile(_pipeHandle, wire, WIRE_SIZE, &written, NULL) || written != WIRE_SIZE)
        {
            ClosePipe();
            memcpy(outBuf, inBuf, sizeof(outBuf));
        }
        else
        {
            DWORD read = 0;
            if (!ReadFile(_pipeHandle, wire, WIRE_SIZE, &read, NULL) || read != WIRE_SIZE)
            {
                ClosePipe();
                memcpy(outBuf, inBuf, sizeof(outBuf));
            }
            else
            {
                memcpy(outBuf, &wire[8], FRAME_SIZE * sizeof(short));
            }
        }

        for (UINT32 i = 0; i < chunk; i++)
            pOutBuf[offset + i] = outBuf[i] / 32768.0f;
    }

    pOutput->u32ValidFrameCount = frameCount;
    pOutput->u32BufferFlags = pInput->u32BufferFlags;
    return;

passthrough:
    memcpy(pOutBuf, pInBuf, frameCount * sizeof(float));
    pOutput->u32ValidFrameCount = frameCount;
    pOutput->u32BufferFlags = pInput->u32BufferFlags;
}

UINT32 STDMETHODCALLTYPE CClearMicApo::CalcInputFrames(UINT32 u32OutputFrameCount)
{
    return u32OutputFrameCount;
}

UINT32 STDMETHODCALLTYPE CClearMicApo::CalcOutputFrames(UINT32 u32InputFrameCount)
{
    return u32InputFrameCount;
}

STDMETHODIMP CClearMicApo::LockForProcess(
    UINT32 u32NumInputConnections,
    APO_CONNECTION_DESCRIPTOR** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_DESCRIPTOR** ppOutputConnections)
{
    if (_isLocked) return APOERR_APO_LOCKED;
    if (u32NumInputConnections != 1 || u32NumOutputConnections != 1)
        return APOERR_NUM_CONNECTIONS_INVALID;
    if (!ppInputConnections || !ppOutputConnections)
        return E_POINTER;

    _isLocked = true;
    return S_OK;
}

STDMETHODIMP CClearMicApo::UnlockForProcess()
{
    _isLocked = false;
    ClosePipe();
    return S_OK;
}

STDMETHODIMP CClearMicApo::IsLocked()
{
    return _isLocked ? S_OK : S_FALSE;
}

HRESULT CClearMicApo::OpenPipe()
{
    ClosePipe();

    _pipeHandle = CreateFileW(
        PIPE_NAME,
        GENERIC_READ | GENERIC_WRITE,
        0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);

    if (_pipeHandle == INVALID_HANDLE_VALUE)
        return HRESULT_FROM_WIN32(GetLastError());

    DWORD mode = PIPE_READMODE_MESSAGE;
    if (!SetNamedPipeHandleState(_pipeHandle, &mode, NULL, NULL))
    {
        ClosePipe();
        return HRESULT_FROM_WIN32(GetLastError());
    }

    return S_OK;
}

void CClearMicApo::ClosePipe()
{
    if (_pipeHandle != INVALID_HANDLE_VALUE)
    {
        CloseHandle(_pipeHandle);
        _pipeHandle = INVALID_HANDLE_VALUE;
    }
}

// ---------------------------------------------------------------------------
// CClearMicApoFactory
// ---------------------------------------------------------------------------

CClearMicApoFactory::CClearMicApoFactory() : _refCount(1)
{
    InterlockedIncrement(&g_cRefDll);
}

CClearMicApoFactory::~CClearMicApoFactory()
{
    InterlockedDecrement(&g_cRefDll);
}

STDMETHODIMP CClearMicApoFactory::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (riid == IID_IUnknown || riid == IID_IClassFactory)
        *ppv = static_cast<IClassFactory*>(this);
    else
        return E_NOINTERFACE;

    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) CClearMicApoFactory::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

STDMETHODIMP_(ULONG) CClearMicApoFactory::Release()
{
    ULONG ref = InterlockedDecrement(&_refCount);
    if (ref == 0) delete this;
    return ref;
}

STDMETHODIMP CClearMicApoFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (pUnkOuter) return CLASS_E_NOAGGREGATION;

    auto* pApo = new (std::nothrow) CClearMicApo();
    if (!pApo) return E_OUTOFMEMORY;

    HRESULT hr = pApo->QueryInterface(riid, ppv);
    pApo->Release();
    return hr;
}

STDMETHODIMP CClearMicApoFactory::LockServer(BOOL fLock)
{
    if (fLock) InterlockedIncrement(&g_cRefDll);
    else InterlockedDecrement(&g_cRefDll);
    return S_OK;
}

// ---------------------------------------------------------------------------
// DLL exports
// ---------------------------------------------------------------------------

STDAPI DllMain(HINSTANCE hModule, DWORD dwReason, LPVOID)
{
    if (dwReason == DLL_PROCESS_ATTACH)
    {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (rclsid != CLSID_ClearMicApo) return CLASS_E_CLASSNOTAVAILABLE;

    auto* pFactory = new (std::nothrow) CClearMicApoFactory();
    if (!pFactory) return E_OUTOFMEMORY;

    HRESULT hr = pFactory->QueryInterface(riid, ppv);
    pFactory->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return g_cRefDll == 0 ? S_OK : S_FALSE;
}

static HRESULT SetRegString(HKEY hKey, LPCWSTR valueName, LPCWSTR value)
{
    return RegSetValueExW(hKey, valueName, 0, REG_SZ,
        (const BYTE*)value, (DWORD)((wcslen(value) + 1) * sizeof(WCHAR)));
}

static HRESULT SetRegDword(HKEY hKey, LPCWSTR valueName, DWORD value)
{
    return RegSetValueExW(hKey, valueName, 0, REG_DWORD, (const BYTE*)&value, sizeof(value));
}

STDAPI DllRegisterServer()
{
    WCHAR modulePath[MAX_PATH];
    if (!GetModuleFileNameW(g_hModule, modulePath, MAX_PATH))
        return HRESULT_FROM_WIN32(GetLastError());

    const WCHAR* clsidStr = L"{7C538B0F-709F-4CBF-8E2A-EBCD11DB6B7B}";

    WCHAR key[256];
    HKEY hKey = nullptr;

    // CLSID\...
    wcscpy_s(key, L"CLSID\\");
    wcscat_s(key, clsidStr);
    if (FAILED(RegCreateKeyExW(HKEY_CLASSES_ROOT, key, 0, NULL,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL)))
        return HRESULT_FROM_WIN32(GetLastError());
    SetRegString(hKey, NULL, L"ClearMic APO");
    RegCloseKey(hKey);

    // CLSID\...\InprocServer32
    wcscpy_s(key, L"CLSID\\");
    wcscat_s(key, clsidStr);
    wcscat_s(key, L"\\InprocServer32");
    if (FAILED(RegCreateKeyExW(HKEY_CLASSES_ROOT, key, 0, NULL,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL)))
        return HRESULT_FROM_WIN32(GetLastError());
    SetRegString(hKey, NULL, modulePath);
    SetRegString(hKey, L"ThreadingModel", L"Both");
    RegCloseKey(hKey);

    // Audio ProcessingObjects
    wcscpy_s(key, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Audio\\ProcessingObjects\\");
    wcscat_s(key, clsidStr);
    if (FAILED(RegCreateKeyExW(HKEY_LOCAL_MACHINE, key, 0, NULL,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL)))
        return HRESULT_FROM_WIN32(GetLastError());
    SetRegString(hKey, NULL, L"ClearMic Noise Reduction");
    SetRegDword(hKey, L"Flags", APO_FLAG_DEFAULT);
    SetRegDword(hKey, L"MaxInputConnections", 1);
    SetRegDword(hKey, L"MaxOutputConnections", 1);
    SetRegDword(hKey, L"MinInputConnectionBufferSize", FRAME_SIZE);
    SetRegDword(hKey, L"MaxInputConnectionBufferSize", FRAME_SIZE);
    SetRegDword(hKey, L"MinOutputConnectionBufferSize", FRAME_SIZE);
    SetRegDword(hKey, L"MaxOutputConnectionBufferSize", FRAME_SIZE);
    RegCloseKey(hKey);

    return S_OK;
}

STDAPI DllUnregisterServer()
{
    const WCHAR* clsidStr = L"{7C538B0F-709F-4CBF-8E2A-EBCD11DB6B7B}";

    WCHAR key[256];

    wcscpy_s(key, L"CLSID\\");
    wcscat_s(key, clsidStr);
    RegDeleteTreeW(HKEY_CLASSES_ROOT, key);

    wcscpy_s(key, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Audio\\ProcessingObjects\\");
    wcscat_s(key, clsidStr);
    RegDeleteTreeW(HKEY_LOCAL_MACHINE, key);

    return S_OK;
}
