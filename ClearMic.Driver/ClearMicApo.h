#pragma once

#include <new>
#include <windows.h>
#include <mmreg.h>
#include <audiomediatype.h>
#include <audioenginebaseapo.h>
#include <audioengineextensionapo.h>
#include <AudioAPOTypes.h>

#define FRAME_SIZE    480
#define PIPE_NAME     L"\\\\.\\pipe\\ClearMic_APO"
#define WIRE_SIZE     (4 + 4 + FRAME_SIZE * 2)

DEFINE_GUID(CLSID_ClearMicApo,
    0x7C538B0F, 0x709F, 0x4CBF, 0x8E, 0x2A, 0xEB, 0xCD, 0x11, 0xDB, 0x6B, 0x7B);

class CClearMicApo : public IAudioProcessingObject,
                     public IAudioProcessingObjectRT,
                     public IAudioProcessingObjectConfiguration,
                     public IAudioSystemEffects
{
public:
    CClearMicApo();
    ~CClearMicApo();

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv);
    STDMETHODIMP_(ULONG) AddRef();
    STDMETHODIMP_(ULONG) Release();

    STDMETHODIMP GetRegistrationProperties(APO_REG_PROPERTIES** ppProperties);
    STDMETHODIMP Initialize(UINT32 cbDataSize, BYTE* pbyData);
    STDMETHODIMP Reset();
    STDMETHODIMP IsInputFormatSupported(
        IAudioMediaType* pOppositeFormat,
        IAudioMediaType* pRequestedInputFormat,
        IAudioMediaType** ppSupportedInputFormat);
    STDMETHODIMP IsOutputFormatSupported(
        IAudioMediaType* pOppositeFormat,
        IAudioMediaType* pRequestedOutputFormat,
        IAudioMediaType** ppSupportedOutputFormat);
    STDMETHODIMP GetInputChannelCount(UINT32* pu32ChannelCount);
    STDMETHODIMP GetLatency(HNSTIME* pTime);

    void STDMETHODCALLTYPE APOProcess(
        UINT32 u32NumInputConnections,
        APO_CONNECTION_PROPERTY** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_PROPERTY** ppOutputConnections);
    UINT32 STDMETHODCALLTYPE CalcInputFrames(UINT32 u32OutputFrameCount);
    UINT32 STDMETHODCALLTYPE CalcOutputFrames(UINT32 u32InputFrameCount);

    STDMETHODIMP LockForProcess(
        UINT32 u32NumInputConnections,
        APO_CONNECTION_DESCRIPTOR** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_DESCRIPTOR** ppOutputConnections);
    STDMETHODIMP UnlockForProcess();
    STDMETHODIMP IsLocked();

private:
    LONG   _refCount;
    bool   _isInitialized;
    bool   _isLocked;
    HANDLE _pipeHandle;

    HRESULT OpenPipe();
    void    ClosePipe();
};

class CClearMicApoFactory : public IClassFactory
{
public:
    CClearMicApoFactory();
    ~CClearMicApoFactory();
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv);
    STDMETHODIMP_(ULONG) AddRef();
    STDMETHODIMP_(ULONG) Release();
    STDMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv);
    STDMETHODIMP LockServer(BOOL fLock);
private:
    LONG _refCount;
};
