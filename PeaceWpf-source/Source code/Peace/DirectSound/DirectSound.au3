#include-once
#include "DirectSoundConstants.au3"
#include <WinApi.au3>
#include <WinAPICom.au3>
#include <Memory.au3>

; #INDEX# =======================================================================================================================
; Title .........: DirectSound incl. DirectX_MediaObject
; AutoIt Version : 3.3.12.0++
; Language ......: English
; Description ...:
; Author ........: Eukalyptus
; Dll ...........: dsound.dll, Msdmo.dll
; ===============================================================================================================================

Global Const $__g_hDSNDDLL = DllOpen("dsound.dll")
Global Const $__g_hMsdmoDLL = DllOpen("Msdmo.dll")

; #CURRENT# =====================================================================================================================
; _DSnd_Create
; _DSnd_CreateSoundBuffer
; _DSnd_CreateSoundBufferEx
; _DSnd_DuplicateSoundBuffer
; _DSnd_Enumerate
; _DSnd_EnumerateEx
; _DSnd_NotifyCreate
; _DSnd_NotifySetPositions
; _DSnd_BufferGetObjectInPath
; _DSnd_BufferLock
; _DSnd_BufferPlay
; _DSnd_BufferSetFX
; _DSnd_BufferUnlock
; _DSnd_CaptureCreate
; _DSnd_CaptureCreateCaptureBuffer
; _DSnd_CaptureEnumerate
; _DSnd_CaptureEnumerateEx
; _DSnd_CaptureGetCaps
; _DSnd_CaptureGetCapsFormat
; _DSnd_CaptureBufferLock
; _DSnd_CaptureBufferUnlock
; _DSnd_WaveLoadFromFile
; _DSnd_MP3Decode
; _DSnd_Seconds2Bytes
; _DSnd_Bytes2Seconds
; _DSnd_Seconds2Time
; _DSnd_Time2Seconds
; _DSnd_SPEAKERCOMBINED
; _DSnd_SPEAKERCONFIG
; _DSnd_SPEAKERGEOMETRY
; _DSnd_ErrorMessage
; _DMO_CreateInstance
; _DMO_QueryInterface
; _DMO_Enum
; _DMO_EnumEx
; _DMO_MediaTypeFree
; _DMO_MediaTypeInit
; _DMO_MediaBufferDispose
; _DMO_MediaBufferCreate
; _DMO_MediaObjectSetInputType
; _DMO_MediaObjectSetOutputType
; _DMO_MediaParamsAddEnvelope
; ===============================================================================================================================

; #INTERNAL_USE_ONLY# ===========================================================================================================
; __DSnd_EnumCallback
; __DSnd_ASMCreate
; ===============================================================================================================================

; ###############################################################################################################################
; # DirectSound
; ###############################################################################################################################

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_Create
; Description ...: Creates and initializes an IDirectSound8 interface.
; Syntax ........: _DSnd_Create([$sGUIDDevice = Null[, $hWnd = 0[, $iLevel = $DSSCL_NORMAL]]])
; Parameters ....: $sGUIDDevice         - GUID that identifies the sound device. The value of this parameter must be one of the GUIDs returned by _DSnd_Enumerate,
;                                         or NULL for the default device, or one of the following values:
;                                                      - $DSDEVID_DefaultPlayback       System-wide default audio playback device. Equivalent to NULL.
;                                                      - $DSDEVID_DefaultVoicePlayback  Default voice playback device.
;                  $hWnd                - Handle to the application window.
;                  $iLevel              - Requested level. Specify one of the following values:
;                                                   - $DSSCL_EXCLUSIVE      Has the same effect as $DSSCL_PRIORITY.
;                                                   - $DSSCL_NORMAL         Sets the normal level. This level has the smoothest multitasking and resource-sharing behavior, but because it does not allow the primary buffer format to change, output is restricted to the default 8-bit format.
;                                                   - $DSSCL_PRIORITY       Sets the priority level. Applications with this cooperative level can call the SetFormat and Compact methods.
;                                                   - $DSSCL_WRITEPRIMARY   Sets the write-primary level. The application has write access to the primary buffer. No secondary buffers can be played. This level cannot be set if the DirectSound driver is being emulated for the device; that is, if the GetCaps method returns the DSCAPS_EMULDRIVER flag in the DSCAPS structure.
; Return values .: Success - An IDirectSound object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _DSnd_Create($sGUIDDevice = Null, $hWnd = 0, $iLevel = $DSSCL_PRIORITY)
	Local $tGUID = Null
	If $sGUIDDevice Then $tGUID = _WinAPI_GUIDFromString($sGUIDDevice)

	Local $aResult = DllCall($__g_hDSNDDLL, "uint", "DirectSoundCreate8", "struct*", $tGUID, "ptr*", 0, "ptr", Null)
	If @error Then Return SetError($DSERR_UFAIL, 0, False)
	If $aResult[0] Or Not $aResult[2] Then Return SetError($aResult[0], 1, False)

	Local $oDSound = ObjCreateInterface($aResult[2], $sIID_IDirectSound8, $tagIDirectSound8)
	If Not IsObj($oDSound) Then Return SetError($DSERR_OBJFAIL, 2, False)

	If Not $hWnd Then $hWnd = _WinAPI_GetDesktopWindow()
	Local $iHResult = $oDSound.SetCooperativeLevel($hWnd, $iLevel)
	If $iHResult Then Return SetError($iHResult, 3, False)

	Return $oDSound
EndFunc   ;==>_DSnd_Create

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CreateSoundBuffer
; Description ...: Creates a sound buffer object to manage audio samples.
; Syntax ........: _DSnd_CreateSoundBuffer($oDSnd[, $iFlags = $DSBCAPS_PRIMARYBUFFER[, $iBufferBytes = 0[, $iChannels = 2[, $iSamplesPerSec = 44100[, $iBitsPerSample = 16[, $iFormatTag = $WAVE_FORMAT_PCM]]]]]])
; Parameters ....: $oDSnd               - This IDirectSound8 object
;                  $iFlags              - Flags specifying the capabilities of the buffer.
;                                               | BitOr one or more of the following values:
;                                                     - $DSBCAPS_CTRL3D                The buffer has 3D control capability.
;                                                     - $DSBCAPS_CTRLFREQUENCY         The buffer has frequency control capability.
;                                                     - $DSBCAPS_CTRLFX                The buffer supports effects processing.
;                                                     - $DSBCAPS_CTRLPAN               The buffer has pan control capability.
;                                                     - $DSBCAPS_CTRLVOLUME            The buffer has volume control capability.
;                                                     - $DSBCAPS_CTRLPOSITIONNOTIFY    The buffer has position notification capability.
;                                                     - $DSBCAPS_GETCURRENTPOSITION2   The buffer uses the new behavior of the play cursor when _DSndBuffer_GetCurrentPosition is called.
;                                                     - $DSBCAPS_GLOBALFOCUS           With this flag set, an application can continue to play its buffers if the user switches focus to another application.
;                                                     - $DSBCAPS_LOCDEFER              The buffer can be assigned to a hardware or software resource at play time, or when _DSndBuffer_AcquireResources is called.
;                                                     - $DSBCAPS_LOCHARDWARE           The buffer uses hardware mixing.
;                                                     - $DSBCAPS_LOCSOFTWARE           The buffer is in software memory and uses software mixing.
;                                                     - $DSBCAPS_MUTE3DATMAXDISTANCE   The sound is reduced to silence at the maximum distance. The buffer will stop playing when the maximum distance is exceeded, so that processor time is not wasted.
;                                                     - $DSBCAPS_PRIMARYBUFFER         The buffer is a primary buffer.
;                                                     - $DSBCAPS_STATIC                The buffer is in on-board hardware memory.
;                                                     - $DSBCAPS_STICKYFOCUS           The buffer has sticky focus. If the user switches to another application not using DirectSound, the buffer is still audible.
;                                                     - $DSBCAPS_TRUEPLAYPOSITION      Force _DSndBuffer8_GetCurrentPosition to return the buffer's true play position. This flag is only valid in Windows Vista.
;                  $iBufferBytes        - Size of the new buffer, in bytes. This value must be 0 when creating a buffer with the $DSBCAPS_PRIMARYBUFFER flag. For secondary buffers, the minimum and maximum sizes allowed are specified by $DSBSIZE_MIN and $DSBSIZE_MAX, defined in DirectSoundConstants.au3.
;                  $iChannels           - Number of channels in the waveform-audio data.
;                  $iSamplesPerSec      - Sample rate, in samples per second (hertz).
;                  $iBitsPerSample      - Bits per sample should be equal to 8 or 16 if $iFormatTag = $WAVE_FORMAT_PCM
;                  $iFormatTag          - Waveform-audio format type.
; Return values .: Success - An IDirectSoundBuffer object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: DirectSound does not initialize the contents of the buffer, and the application cannot assume that it contains silence. If an attempt is made to create a buffer with the DSBCAPS_LOCHARDWARE flag on a system where hardware acceleration is not available, the method fails with either DSERR_CONTROLUNAVAIL or DSERR_INVALIDCALL, depending on the operating system.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _DSnd_CreateSoundBuffer($oDSnd, $iFlags = $DSBCAPS_PRIMARYBUFFER, $iBufferBytes = 0, $iChannels = 2, $iSamplesPerSec = 44100, $iBitsPerSample = 16, $iFormatTag = $WAVE_FORMAT_PCM)
	If Not IsObj($oDSnd) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Local $iAvgBytesPerSec = $iSamplesPerSec * $iBlockAlign

	Local $tWaveFormatEx = DllStructCreate($tagDSWAVEFORMATEX)
	$tWaveFormatEx.FormatTag = $iFormatTag
	$tWaveFormatEx.Channels = $iChannels
	$tWaveFormatEx.SamplesPerSec = $iSamplesPerSec
	$tWaveFormatEx.AvgBytesPerSec = $iAvgBytesPerSec
	$tWaveFormatEx.BlockAlign = $iBlockAlign
	$tWaveFormatEx.BitsPerSample = $iBitsPerSample

	Local $tDS_BufferDesc = DllStructCreate($tagDSBUFFERDESC)
	$tDS_BufferDesc.Size = DllStructGetSize($tDS_BufferDesc)
	$tDS_BufferDesc.Flags = $iFlags
	If Not BitAND($iFlags, $DSBCAPS_PRIMARYBUFFER) Then
		$tDS_BufferDesc.BufferBytes = $iBufferBytes
		$tDS_BufferDesc.WaveFormatEX = DllStructGetPtr($tWaveFormatEx)
	EndIf

	Local $pDS_Buffer
	Local $iHResult = $oDSnd.CreateSoundBuffer($tDS_BufferDesc, $pDS_Buffer, Null)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $oDS_Buffer
	If BitAND($iFlags, $DSBCAPS_PRIMARYBUFFER) Then
		$oDS_Buffer = ObjCreateInterface($pDS_Buffer, $sIID_IDirectSoundBuffer, $tagIDirectSoundBuffer)
	Else
		$oDS_Buffer = ObjCreateInterface($pDS_Buffer, $sIID_IDirectSoundBuffer8, $tagIDirectSoundBuffer8)
	EndIf
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_OBJFAIL, 2, False)

	If BitAND($iFlags, $DSBCAPS_PRIMARYBUFFER) Then
		$iHResult = $oDS_Buffer.SetFormat($tWaveFormatEx)
		If $iHResult Then Return SetError($iHResult, 3, $oDS_Buffer)
	EndIf

	Return $oDS_Buffer
EndFunc   ;==>_DSnd_CreateSoundBuffer

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CreateSoundBufferEx
; Description ...: Creates a sound buffer object to manage audio samples using WAVEFORMATEXTENSIBLE
; Syntax ........: _DSnd_CreateSoundBufferEx($oDSnd, $iFlags, $iBufferBytes[, $iChannels = 2[, $iSamplesPerSec = 44100[, $iBitsPerSample = 32[, $iFormatTag = $WAVE_FORMAT_IEEE_FLOAT[, $iValidBitsPerSample = 0[, $iChannelMask = 0[, $sGUIDSubFormat = ""]]]]]]])
; Parameters ....: $oDSnd               - This IDirectSound8 object
;                  $iFlags              - Flags specifying the capabilities of the buffer.
;                                               | BitOr one or more of the following values:
;                                                     - $DSBCAPS_CTRL3D                The buffer has 3D control capability.
;                                                     - $DSBCAPS_CTRLFREQUENCY         The buffer has frequency control capability.
;                                                     - $DSBCAPS_CTRLFX                The buffer supports effects processing.
;                                                     - $DSBCAPS_CTRLPAN               The buffer has pan control capability.
;                                                     - $DSBCAPS_CTRLVOLUME            The buffer has volume control capability.
;                                                     - $DSBCAPS_CTRLPOSITIONNOTIFY    The buffer has position notification capability.
;                                                     - $DSBCAPS_GETCURRENTPOSITION2   The buffer uses the new behavior of the play cursor when _DSndBuffer_GetCurrentPosition is called.
;                                                     - $DSBCAPS_GLOBALFOCUS           With this flag set, an application can continue to play its buffers if the user switches focus to another application.
;                                                     - $DSBCAPS_LOCDEFER              The buffer can be assigned to a hardware or software resource at play time, or when _DSndBuffer_AcquireResources is called.
;                                                     - $DSBCAPS_LOCHARDWARE           The buffer uses hardware mixing.
;                                                     - $DSBCAPS_LOCSOFTWARE           The buffer is in software memory and uses software mixing.
;                                                     - $DSBCAPS_MUTE3DATMAXDISTANCE   The sound is reduced to silence at the maximum distance. The buffer will stop playing when the maximum distance is exceeded, so that processor time is not wasted.
;                                                     - $DSBCAPS_PRIMARYBUFFER         The buffer is a primary buffer.
;                                                     - $DSBCAPS_STATIC                The buffer is in on-board hardware memory.
;                                                     - $DSBCAPS_STICKYFOCUS           The buffer has sticky focus. If the user switches to another application not using DirectSound, the buffer is still audible.
;                                                     - $DSBCAPS_TRUEPLAYPOSITION      Force _DSndBuffer8_GetCurrentPosition to return the buffer's true play position. This flag is only valid in Windows Vista.
;                  $iBufferBytes        - Size of the new buffer, in bytes. This value must be 0 when creating a buffer with the $DSBCAPS_PRIMARYBUFFER flag. For secondary buffers, the minimum and maximum sizes allowed are specified by $DSBSIZE_MIN and $DSBSIZE_MAX, defined in DirectSoundConstants.au3.
;                  $iChannels           - Number of channels in the waveform-audio data.
;                  $iSamplesPerSec      - Sample rate, in samples per second (hertz).
;                  $iBitsPerSample      - Bits per sample. Must be a multiple of 8
;                  $iFormatTag          - Waveform-audio format type.
;                  $iValidBitsPerSample - Number of bits of precision in the signal. Usually equal to $iBitsPerSample. However, $iBitsPerSample is the container size and must be a multiple of 8, whereas $iValidBitsPerSample can be any value not exceeding the container size. For example, if the format uses 20-bit samples, $iBitsPerSample must be at least 24, but $iValidBitsPerSample is 20.
;                  $iChannelMask        - Bitmask specifying the assignment of channels in the stream to speaker positions.
;                                               | BitOr one or more of the following values:
;                                                     - §SPEAKER_FRONT_LEFT             0x1
;                                                     - §SPEAKER_FRONT_RIGHT            0x2
;                                                     - §SPEAKER_FRONT_CENTER           0x4
;                                                     - §SPEAKER_LOW_FREQUENCY          0x8
;                                                     - §SPEAKER_BACK_LEFT              0x10
;                                                     - §SPEAKER_BACK_RIGHT             0x20
;                                                     - §SPEAKER_FRONT_LEFT_OF_CENTER   0x40
;                                                     - §SPEAKER_FRONT_RIGHT_OF_CENTER  0x80
;                                                     - §SPEAKER_BACK_CENTER            0x100
;                                                     - §SPEAKER_SIDE_LEFT              0x200
;                                                     - §SPEAKER_SIDE_RIGHT             0x400
;                                                     - §SPEAKER_TOP_CENTER             0x800
;                                                     - §SPEAKER_TOP_FRONT_LEFT         0x1000
;                                                     - §SPEAKER_TOP_FRONT_CENTER       0x2000
;                                                     - §SPEAKER_TOP_FRONT_RIGHT        0x4000
;                                                     - §SPEAKER_TOP_BACK_LEFT          0x8000
;                                                     - §SPEAKER_TOP_BACK_CENTER        0x10000
;                                                     - §SPEAKER_TOP_BACK_RIGHT         0x20000
;                  $sGUIDSubFormat      - Subformat of the data, such as $KSDATAFORMAT_SUBTYPE_PCM.
; Return values .: Success - An IDirectSoundBuffer object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: DirectSound does not initialize the contents of the buffer, and the application cannot assume that it contains silence. If an attempt is made to create a buffer with the DSBCAPS_LOCHARDWARE flag on a system where hardware acceleration is not available, the method fails with either DSERR_CONTROLUNAVAIL or DSERR_INVALIDCALL, depending on the operating system.
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CreateSoundBufferEx($oDSnd, $iFlags, $iBufferBytes, $iChannels = 2, $iSamplesPerSec = 44100, $iBitsPerSample = 32, $iFormatTag = $WAVE_FORMAT_IEEE_FLOAT, $iValidBitsPerSample = 0, $iChannelMask = 0, $sGUIDSubFormat = "")
	If Not IsObj($oDSnd) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Local $iAvgBytesPerSec = $iSamplesPerSec * $iBlockAlign

	Local $tWaveFormatExtensible = DllStructCreate($tagDSWAVEFORMATEXTENSIBLE)
	$tWaveFormatExtensible.FormatTag = $iFormatTag
	$tWaveFormatExtensible.Channels = $iChannels
	$tWaveFormatExtensible.SamplesPerSec = $iSamplesPerSec
	$tWaveFormatExtensible.AvgBytesPerSec = $iAvgBytesPerSec
	$tWaveFormatExtensible.BlockAlign = $iBlockAlign
	$tWaveFormatExtensible.BitsPerSample = $iBitsPerSample
	If $iValidBitsPerSample Or $iChannelMask Or $sGUIDSubFormat Then
		$tWaveFormatExtensible.Size = 22
		$tWaveFormatExtensible.ValidBitsPerSample = $iValidBitsPerSample
		$tWaveFormatExtensible.ChannelMask = $iChannelMask
		_WinAPI_GUIDFromStringEx($sGUIDSubFormat, DllStructGetPtr($tWaveFormatExtensible, "SubFormat"))
	EndIf

	Local $tDS_BufferDesc = DllStructCreate($tagDSBUFFERDESC)
	$tDS_BufferDesc.Size = DllStructGetSize($tDS_BufferDesc)
	$tDS_BufferDesc.Flags = $iFlags
	$tDS_BufferDesc.BufferBytes = $iBufferBytes
	$tDS_BufferDesc.WaveFormatEX = DllStructGetPtr($tWaveFormatExtensible)

	Local $pDS_Buffer
	Local $iHResult = $oDSnd.CreateSoundBuffer($tDS_BufferDesc, $pDS_Buffer, Null)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $oDS_Buffer = ObjCreateInterface($pDS_Buffer, $sIID_IDirectSoundBuffer8, $tagIDirectSoundBuffer8)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_OBJFAIL, 2, False)

	Return $oDS_Buffer
EndFunc   ;==>_DSnd_CreateSoundBufferEx

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_DuplicateSoundBuffer
; Description ...: The DuplicateSoundBuffer method creates a new secondary buffer that shares the original buffer's memory.
; Syntax ........: _DSnd_DuplicateSoundBuffer($oDSnd_, $oDSBufferOriginal, $oRETURN_DSBufferDuplicate)
; Parameters ....: $oDSnd_                       - This IDirectSound8 object
;                  $oDSBufferOriginal            - IDirectSoundBuffer8 object to duplicate.
; Return values .: Success - An IDirectSoundBuffer8 object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: This method is not valid for buffers created with the $DSBCAPS_CTRLFX flag. Initially, the duplicate buffer will have the same parameters as the original buffer. However, the application can change the parameters of each buffer independently, and each can be played or stopped without affecting the other.The buffer memory is released when the last object referencing it is released.There is a known issue with volume levels of duplicated buffers. The duplicated buffer will play at full volume unless you change the volume to a different value than the original buffer's volume setting. If the volume stays the same (even if you explicitly set the same volume in the duplicated buffer with a IDirectSoundBuffer8::SetVolume call), the buffer will play at full volume regardless. To work around this problem, immediately set the volume of the duplicated buffer to something slightly different than what it was, even if you change it one millibel. The volume may then be immediately set back again to the original desired value.
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_DuplicateSoundBuffer($oDSnd, $oDSBufferOriginal)
	If Not IsObj($oDSnd) Then Return SetError($DSERR_NOOBJ, 0, False)
	If Not IsObj($oDSBufferOriginal) Then Return SetError($DSERR_NOOBJ, 1, False)

	Local $pDS_Buffer
	Local $iHResult = $oDSnd.DuplicateSoundBuffer($oDSBufferOriginal, $pDS_Buffer)
	If $iHResult Then Return SetError($iHResult, 2, False)

	Local $oDS_Buffer = ObjCreateInterface($pDS_Buffer, $sIID_IDirectSoundBuffer8, $tagIDirectSoundBuffer8)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_OBJFAIL, 3, False)

	Return $oDS_Buffer
EndFunc   ;==>_DSnd_DuplicateSoundBuffer

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_Enumerate
; Description ...: Enumerates the DirectSound drivers installed in the system.
; Syntax ........: _DSnd_Enumerate()
; Parameters ....:
; Return values .: Success - A two-dimensional array.
;                                $aArray[0][0] = Number of devices
;                                $aArray[1][0] = Description of the first device.
;                                $aArray[1][1] = Module name of the first device.
;                                $aArray[1][2] = GUID of the first device
;                                $aArray[1][0] = Description of the second device.
;                                $aArray[1][1] = Module name of the second device.
;                                $aArray[1][2] = GUID of the second device
;                                ...
;                                $aArray[n][0] = Description of the nth device.
;                                $aArray[n][1] = Module name of the nth device.
;                                $aArray[n][2] = GUID of the nth device
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: GUID can be passed to _DSnd_Create to create a device object for that driver.
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_Enumerate()
	Local $hCallBack = DllCallbackRegister("__DSnd_EnumCallback", $DSEnumCallback_Return, $DSEnumCallback_Params)
	If @error Or Not $hCallBack Then SetError($DSERR_UFAIL, 0, False)

	Local $tUserData = DllStructCreate("uint Cnt; ptr GUID[32]; wchar Desc[8192]; wchar Module[8192];")

	_DSnd_EnumerateEx(DllCallbackGetPtr($hCallBack), $tUserData)
	If @error Then
		DllCallbackFree($hCallBack)
		Return SetError($DSERR_UFAIL, 1, False)
	EndIf

	Local $aRet[$tUserData.Cnt + 1][3] = [[$tUserData.Cnt, "Module", "GUID"]]
	Local $tString
	For $i = 1 To $tUserData.Cnt
		$tString = DllStructCreate("wchar[256];", DllStructGetPtr($tUserData, "Desc") + ($i - 1) * 512)
		$aRet[$i][0] = DllStructGetData($tString, 1)

		$tString = DllStructCreate("wchar[256];", DllStructGetPtr($tUserData, "Module") + ($i - 1) * 512)
		$aRet[$i][1] = DllStructGetData($tString, 1)

		If $tUserData.GUID(($i)) Then $aRet[$i][2] = _WinAPI_StringFromGUID($tUserData.GUID(($i)))
	Next

	Return $aRet
EndFunc   ;==>_DSnd_Enumerate

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_EnumerateEx
; Description ...: Enumerates the DirectSound drivers installed in the system.
; Syntax ........: _DSnd_EnumerateEx($pDSEnumCallback[, $tUserData = Null])
; Parameters ....: $pDSEnumCallback     - Pointer of a DSEnumCallback function that will be called for each device installed in the system.
;                  $tUserData           - Pointer of the user-defined context passed to the enumeration callback function every time that function is called.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_EnumerateEx($pDSEnumCallback, $tUserData = Null)
	If Not IsPtr($pDSEnumCallback) Then Return SetError($DSERR_NOPTR, 0, False)

	Local $aResult = DllCall($__g_hDSNDDLL, "uint", "DirectSoundEnumerate", "struct*", $pDSEnumCallback, "struct*", $tUserData)
	If @error Then Return SetError($DSERR_UFAIL, 1, False)
	If $aResult[0] Then Return SetError($aResult[0], 2, False)

	Return True
EndFunc   ;==>_DSnd_EnumerateEx

; _DSnd_GetCaps
; _DSnd_GetSpeakerConfig
; _DSnd_Initialize
; _DSnd_SetCooperativeLevel
; _DSnd_SetSpeakerConfig
; _DSnd_VerifyCertification

; ###############################################################################################################################
; # DirectSound_Notify / FullDuplex
; ###############################################################################################################################

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_NotifyCreate
; Description ...: Creates an IDirectSoundNotify interface to set up notification events for a playback or capture buffer.
; Syntax ........: _DSnd_NotifyCreate($oDS_Buffer)
; Parameters ....: $oDS_Buffer          - An IDirectSoundBuffer or IDirectSoundCaptureBuffer object
; Return values .: Success - An IDirectSoundNotify object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_NotifyCreate($oDS_Buffer)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $tIID = _WinAPI_GUIDFromString($sIID_IDirectSoundNotify)

	Local $pNotify
	Local $iHResult = $oDS_Buffer.QueryInterface($tIID, $pNotify)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $oNotify = ObjCreateInterface($pNotify, $sIID_IDirectSoundNotify, $tagIDirectSoundNotify)
	If Not IsObj($oNotify) Then Return SetError($DSERR_OBJFAIL, 2, False)

	Return $oNotify
EndFunc   ;==>_DSnd_NotifyCreate

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_NotifySetPositions
; Description ...: Sets the notification positions.
; Syntax ........: _DSnd_NotifySetPositions($oNotify, $aPositions)
; Parameters ....: $oNotify             - An IDirectSoundNotify object
;                  $aPositions          - An Array:
;                                             - $aPositions[0][0] = Number of NotificationPoints
;                                             - $aPositions[1][0] = First NotificationPoint-Offset in bytes or $DSBPN_OFFSETSTOP
;                                             - $aPositions[1][1] = _WinAPI_CreateEvent()-Handle to the event to be signaled
;                                               ...
;                                             - $aPositions[n][0] = n^th NotificationPoint-Offset in bytes or $DSBPN_OFFSETSTOP
;                                             - $aPositions[n][1] = Handle to the event to be signaled
; Return values .: Success - A DllStruct:
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: How the returned DllStruct can be used:
;                       - _WinAPI_WaitForMultipleObjects($tNotify.Cnt, $tNotify.Events, False, 100) ;100 = TimeOut
;                       - _WinAPI_ResetEvent($tNotify.hEvent((Index)))
;                       - _WinAPI_CloseHandle($tNotify.hEvent((Index)))
;                  During capture or playback, whenever the read or play cursor reaches one of the specified offsets, the associated event is signaled.
;                  The $DSBPN_OFFSETSTOP value causes the event to be signaled when playback or capture stops, either because the end of the buffer has been reached (and playback or capture is not looping) or because the application called the IDirectSoundBuffer.Stop method.
; Related .......: _WinAPI_CreateEvent, _WinAPI_CloseHandle, _WinAPI_WaitForMultipleObjects
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_NotifySetPositions($oNotify, $aPositions)
	If Not IsObj($oNotify) Then Return SetError($DSERR_NOOBJ, 0, False)
	If UBound($aPositions, 0) <> 2 Or UBound($aPositions, 2) < 2 Then Return SetError($DSERR_PARAM, 1, False)

	Local $iCnt = $aPositions[0][0], $bMAX = False
	If $iCnt >= UBound($aPositions) Then $iCnt = UBound($aPositions) - 1
	If $iCnt < 1 Then SetError($DSERR_PARAM, 2, False)

	If $iCnt > 64 Then
		$bMAX = True
		$iCnt = 64
	EndIf

	Local $tNotify = DllStructCreate("uint;handle;")
	Local $iSizeOf = DllStructGetSize($tNotify)

	$tNotify = DllStructCreate("handle hEvent[" & $iCnt & "]; ptr Events; uint Cnt; byte Data[" & $iSizeOf * $iCnt & "];")
	Local $pData = DllStructGetPtr($tNotify, "Data")
	DllStructSetData($tNotify, "Events", DllStructGetPtr($tNotify))
	DllStructSetData($tNotify, "Cnt", $iCnt)

	Local $tPosition
	For $i = 1 To $iCnt
		DllStructSetData($tNotify, 1, $aPositions[$i][1], $i)
		$tPosition = DllStructCreate("uint; handle;", $pData + ($i - 1) * $iSizeOf)
		DllStructSetData($tPosition, 1, $aPositions[$i][0])
		DllStructSetData($tPosition, 2, $aPositions[$i][1])
	Next

	Local $iHResult = $oNotify.SetNotificationPositions($iCnt, $pData)
	If $iHResult Then Return SetError($iHResult, 3, False)

	If $bMAX Then Return SetExtended(-1, $tNotify)
	Return $tNotify
EndFunc   ;==>_DSnd_NotifySetPositions

; _DSnd_FullDuplexInitialize

; ###############################################################################################################################
; # DirectSound_Buffer
; ###############################################################################################################################
; _DSnd_BufferAcquireResources
; _DSnd_BufferGetCaps
; _DSnd_BufferGetCurrentPosition
; _DSnd_BufferGetFormat
; _DSnd_BufferGetFrequency

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_BufferGetObjectInPath
; Description ...: Retrieves an interface for an effect object associated with the buffer.
; Syntax ........: _DSnd_BufferGetObjectInPath($oDS_Buffer, $iIndex, $sIID, $tagIntF[, $sGUID = $sGUID_All_Objects])
; Parameters ....: $oDS_Buffer          - This IDirectSoundBuffer8 object
;                  $iIndex              - Index of the object within objects of $sGUID class in the path. (index of the object within the array of effects passed to SetFX)
;                  $sIID                - Unique identifier of the desired interface.
;                  $tagIntF             - Interface description of the desired interface.
;                  $sGUID               - Unique class identifier of the object being searched for, such as GUID_DSFX_STANDARD_ECHO. Set this parameter to GUID_All_Objects to search for objects of any class.
; Return values .: Success - A $sIID - Object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: Any DMO that has been set on a buffer by using SetFX can be retrieved, even it has not been allocated resources.
;                  The following interfaces can be retrieved for the various DMOs supplied with DirectX:
;                              - [$sIID/$tag]_IDirectSoundFXGargle
;                              - [$sIID/$tag]_IDirectSoundFXChorus
;                              - [$sIID/$tag]_IDirectSoundFXFlanger
;                              - [$sIID/$tag]_IDirectSoundFXEcho
;                              - [$sIID/$tag]_IDirectSoundFXDistortion
;                              - [$sIID/$tag]_IDirectSoundFXCompressor
;                              - [$sIID/$tag]_IDirectSoundFXParamEq
;                              - [$sIID/$tag]_IDirectSoundFXWavesReverb
;                              - [$sIID/$tag]_IDirectSoundFXI3DL2Reverb
;
;                  In addition, the following interfaces are available for any of the standard DMOs:
;                              - [$sIID/$tag]_IMediaObject
;                              - [$sIID/$tag]_IMediaObjectInPlace
;                              - [$sIID/$tag]_IMediaParams
;                                    | NoteWhen the DirectSound API is used to play buffers, parameter curves (envelopes) set by using the IMediaParams interface do not work, because DirectSound does not timestamp the DMO buffers.
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_BufferGetObjectInPath($oDS_Buffer, $iIndex, $sIID, $tagIntF, $sGUID = $sGUID_All_Objects)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $tGUID = _WinAPI_GUIDFromString($sGUID)
	Local $tIID = _WinAPI_GUIDFromString($sIID)

	Local $pObj
	Local $iHResult = $oDS_Buffer.GetObjectInPath($tGUID, $iIndex, $tIID, $pObj)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $oObj = ObjCreateInterface($pObj, $sIID, $tagIntF)
	If Not IsObj($oObj) Then Return SetError($DSERR_OBJFAIL, 2, False)

	Return $oObj
EndFunc   ;==>_DSnd_BufferGetObjectInPath

; _DSnd_BufferGetPan
; _DSnd_BufferGetStatus
; _DSnd_BufferGetVolume
; _DSnd_BufferInitialize

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_BufferLock
; Description ...: Readies all or part of the buffer for a data write and returns pointers to which data can be written.
; Syntax ........: _DSnd_BufferLock($oDS_Buffer[, $iLockBytes = 0[, $iFlags = $DSBLOCK_ENTIREBUFFER[, $iOffset = 0]]])
; Parameters ....: $oDS_Buffer          - This IDirectSoundBuffer8 object
;                  $iLockBytes          - Size, in bytes, of the portion of the buffer to lock. The buffer is conceptually circular, so this number can exceed the number of bytes between $iOffset and the end of the buffer.
;                  $iFlags              - Flags modifying the lock event. The following flags are defined:
;                                                   - $DSBLOCK_FROMWRITECURSOR     Start the lock at the write cursor. The $iOffset parameter is ignored.
;                                                   - $DSBLOCK_ENTIREBUFFER        Lock the entire buffer. The $iBytes parameter is ignored.
;                  $iOffset             - Offset, in bytes, from the start of the buffer to the point where the lock begins. This parameter is ignored if $DSBLOCK_FROMWRITECURSOR is specified in the $iFlags parameter.
; Return values .: Success - A one-dimensional array.
;                                $aLock[0] = Pointer to the first locked part of the buffer.
;                                $aLock[1] = Number of bytes in the block at $aLock[0]. If this value is less than $iBytes, the lock has wrapped and $aLock[2] points to a second block of data at the beginning of the buffer.
;                                $aLock[2] = Pointer to the second locked part of the capture buffer. If NULL is returned, the $aLock[0] parameter points to the entire locked portion of the capture buffer.
;                                $aLock[3] = Number of bytes in the block at $aLock[2]. If $aLock[2] is NULL, this value is zero.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: This method accepts an offset and a byte count, and returns two write pointers($aLock[0], $aLock[2]) and their associated sizes.($aLock[1], $aLock[3])
;                  If the locked portion does not extend to the end of the buffer and wrap to the beginning, the second pointer, $aLock[2], receives NULL.
;
;                       - Scenario 1: WriteCursor < PlayCursor
;                                                     WriteCursor                      PlayCursor
;                                                        |                                 |>
;                                       Buffer: [********##################################********************************************]
;                                                        |________________________________|
;                                                        |
;                                                     $aLock[0]------- $aLock[1] Bytes ---|
;
;                       - Scenario 2: WriteCursor > PlayCursor
;                                                                     PlayCursor                        WriteCursor
;                                                                          |>                                |
;                                       Buffer: [##########################**********************************##########################]
;                                                |________________________|                                  |________________________|
;                                                |                                                           |
;                                           $aLock[2]-- $aLock[3] Bytes --|                             $aLock[0]-- $aLock[1] Bytes --|
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _DSnd_BufferLock($oDS_Buffer, $iLockBytes = 0, $iFlags = $DSBLOCK_ENTIREBUFFER, $iOffset = 0)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $pBuffer, $iSize, $pBuffer2, $iSize2
	Local $iHResult = $oDS_Buffer.Lock($iOffset, $iLockBytes, $pBuffer, $iSize, $pBuffer2, $iSize2, $iFlags)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $aLock[4]
	$aLock[0] = $pBuffer
	$aLock[1] = $iSize
	$aLock[2] = $pBuffer2
	$aLock[3] = $iSize2
	Return $aLock
EndFunc   ;==>_DSnd_BufferLock

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_BufferPlay
; Description ...: Causes the sound buffer to play, starting at the play cursor.
; Syntax ........: _DSnd_BufferPlay($oDS_Buffer[, $iFlag = 0[, $iPriority = 0]])
; Parameters ....: $oDS_Buffer          - This IDirectSoundBuffer8 object
;                  $iFlag               - Flags specifying how to play the buffer. The following flags are defined:
;                                                   - $DSBPLAY_LOOPING                 After the end of the audio buffer is reached, play restarts at the beginning of the buffer. Play continues until explicitly stopped. This flag must be set when playing a primary buffer.
;                                                   - $DSBPLAY_LOCHARDWARE             Play this voice in a hardware buffer only. If the hardware has no available voices and no voice management flags are set, the call to _DSndBuffer_Play fails. This flag cannot be combined with $DSBPLAY_LOCSOFTWARE.
;                                                   - $DSBPLAY_LOCSOFTWARE             Play this voice in a software buffer only. This flag cannot be combined with $DSBPLAY_LOCHARDWARE or any voice management flag.
;                                                   - $DSBPLAY_TERMINATEBY_TIME        If the hardware has no available voices, a currently playing nonlooping buffer will be stopped to make room for the new buffer. The buffer prematurely terminated is the one with the least time left to play.
;                                                   - $DSBPLAY_TERMINATEBY_DISTANCE    If the hardware has no available voices, a currently playing buffer will be stopped to make room for the new buffer. The buffer prematurely terminated will be selected from buffers that have the buffer's DSBCAPS_ MUTE3DATMAXDISTANCE flag set and are beyond their maximum distance. If there are no such buffers, the method fails.
;                                                   - $DSBPLAY_TERMINATEBY_PRIORITY    If the hardware has no available voices, a currently playing buffer will be stopped to make room for the new buffer. The buffer prematurely terminated will be the one with the lowest priority as set by the $iPriority parameter passed to _DSndBuffer_Play for the buffer.
;                  $iPriority           - Priority for the sound, used by the voice manager when assigning hardware mixing resources. The lowest priority is 0, and the highest priority is 0xFFFFFFFF. If the buffer was not created with the $DSBCAPS_LOCDEFER flag, this value must be 0.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _DSnd_BufferPlay($oDS_Buffer, $iFlag = 0, $iPriority = 0)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)
	Local $iHResult = $oDS_Buffer.Play(0, $iPriority, $iFlag)
	If $iHResult Then Return SetError($iHResult, 1, False)
	Return True
EndFunc   ;==>_DSnd_BufferPlay

; _DSnd_BufferRestore
; _DSnd_BufferSetCurrentPosition
; _DSnd_BufferSetFormat
; _DSnd_BufferSetFrequency

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_BufferSetFX
; Description ...: Enables effects on a buffer.
; Syntax ........: _DSnd_BufferSetFX($oDS_Buffer, $aFX)
; Parameters ....: $oDS_Buffer          - This IDirectSoundBuffer8 object
;                  $aFX                 - An Array:
;                                             - $aFX[0] = Number of Effects
;                                             - $aFX[1] = first $sGUID_DSFX...
;                                               ...
;                                             - $aFX[n] = n^th $sGUID_DSFX...
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: For the method to succeed, the buffer must have been created with the $DSBCAPS_CTRLFX flag and must not be playing or locked.
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_BufferSetFX($oDS_Buffer, $aFX)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)
	Local $iHResult

	If Not IsArray($aFX) Then
		$iHResult = $oDS_Buffer.SetFX(0, Null, Null)
		If $iHResult Then Return SetError($iHResult, 2, False)
		Return True
	EndIf

	If UBound($aFX, 0) <> 1 Then Return SetError($DSERR_PARAM, 1, False)

	Local $iCnt = $aFX[0]
	If $iCnt >= UBound($aFX) Then $iCnt = UBound($aFX) - 1
	If $iCnt < 1 Then
		$iHResult = $oDS_Buffer.SetFX(0, Null, Null)
		If $iHResult Then Return SetError($iHResult, 2, False)
		Return True
	EndIf

	Local $tFXDesc = DllStructCreate($tagDSEFFECTDESC)
	Local $iSizeOf = DllStructGetSize($tFXDesc)
	$tFXDesc = DllStructCreate("byte[" & $iCnt * $iSizeOf & "];")
	Local $pFXDesc = DllStructGetPtr($tFXDesc)


	Local $tSet
	For $i = 1 To $iCnt
		$tSet = DllStructCreate($tagDSEFFECTDESC, $pFXDesc + ($i - 1) * $iSizeOf)
		$tSet.Size = $iSizeOf
		_WinAPI_GUIDFromStringEx($aFX[$i], DllStructGetPtr($tSet, "DSFXClass"))
	Next

	Local $tError = DllStructCreate("uint[" & $iCnt & "];")
	$iHResult = $oDS_Buffer.SetFX($iCnt, $tFXDesc, $tError)
	If $iHResult Then Return SetError($iHResult, 3, $tError)

	Return True
EndFunc   ;==>_DSnd_BufferSetFX

; _DSnd_BufferSetPan
; _DSnd_BufferSetVolume
; _DSnd_BufferStop

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_BufferUnlock
; Description ...: Releases a locked sound buffer.
; Syntax ........: _DSndBuffer_Unlock($oDS_Buffer, Byref $aLock)
; Parameters ....: $oDS_Buffer          - This IDirectSoundBuffer8 object
;                  $aLock               - Array as returned by _DSnd_BufferLock
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: The values in $aLock[1] and %aLock[3] must specify the number of bytes actually written to each part of the buffer, which might be less than the size of the lock.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _DSnd_BufferUnlock($oDS_Buffer, ByRef $aLock)
	If Not IsObj($oDS_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)
	If Not IsArray($aLock) Or UBound($aLock) <> 4 Then Return SetError($DSERR_PARAM, 1, False)

	Local $iHResult = $oDS_Buffer.Unlock($aLock[0], $aLock[1], $aLock[2], $aLock[3])
	If $iHResult Then Return SetError($iHResult, 2, False)

	$aLock = 0

	Return True
EndFunc   ;==>_DSnd_BufferUnlock

; ###############################################################################################################################
; # DirectSound_Capture
; ###############################################################################################################################

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureCreate
; Description ...: Creates and initializes an IDirectSoundCapture8 interface.
; Syntax ........: _DSnd_CaptureCreate([$sGUIDDevice = Null])
; Parameters ....: $sGUIDDevice         - GUID that identifies the sound capture device. The value of this parameter must be one of the GUIDs returned by _DSnd_CaptureEnumerate,
;                                         or NULL for the default device, or one of the following values:
;                                                      - $DSDEVID_DefaultCapture      = System-wide default audio capture device.
;                                                      - $DSDEVID_DefaultVoiceCapture = Default voice capture device.
; Return values .: Success - An IDirectSoundCapture object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureCreate($sGUIDDevice = Null)
	Local $tGUID = Null
	If $sGUIDDevice Then $tGUID = _WinAPI_GUIDFromString($sGUIDDevice)

	Local $aResult = DllCall($__g_hDSNDDLL, "uint", "DirectSoundCaptureCreate8", "struct*", $tGUID, "ptr*", 0, "ptr", Null)
	If @error Then Return SetError($DSERR_UFAIL, 0, False)
	If $aResult[0] Or Not $aResult[2] Then Return SetError($aResult[0], 1, False)

	Local $oDS_Cap = ObjCreateInterface($aResult[2], $sIID_IDirectSoundCapture, $tagIDirectSoundCapture)
	If Not IsObj($oDS_Cap) Then Return SetError($DSERR_OBJFAIL, 2, False)

	Return $oDS_Cap
EndFunc   ;==>_DSnd_CaptureCreate

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureCreateCaptureBuffer
; Description ...: Creates a buffer for capturing waveform audio.
; Syntax ........: _DSnd_CaptureCreateCaptureBuffer($oDS_Cap, $iBufferBytes[, $iChannels = 2[, $iSamplesPerSec = 44100[,
;                  $iBitsPerSample = 16[, $iFormatTag = $WAVE_FORMAT_PCM[, $bWaveMapper = False[, $aFX = Null]]]]]])
; Parameters ....: $oDS_Cap             - An IDirectSoundCapture object
;                  $iBufferBytes        - Size of capture buffer to create, in bytes.
;                  $iChannels           - Number of channels in the waveform-audio data.
;                  $iSamplesPerSec      - Sample rate, in samples per second (hertz).
;                  $iBitsPerSample      - Bits per sample for the wFormatTag format type.
;                  $iFormatTag          - Waveform-audio format type.
;                  $bWaveMapper         - The Win32 wave mapper will be used for formats not supported by the device.
;                  $aFX                 - An array of effects identifiers:
;                                                      - $aFX[0][0] = Number of effects
;                                                      - $aFX[1][0] = sGUID that specifies the class identifier of the first effect
;                                                                                     - $GUID_DSCFX_CLASS_AEC = Acoustic echo cancellation.
;                                                                                     - $GUID_DSCFX_CLASS_NS  = Noise suppression.

;                                                      - $aFX[1][1] = sGUID that specifies the unique identifier of the preferred effect.
;                                                                                     - $GUID_DSCFX_MS_AEC     = Microsoft acoustic echo cancellation. Available in software only.
;                                                                                     - $GUID_DSCFX_MS_NS      = Microsoft noise suppression. Available in software only.
;                                                                                     - $GUID_DSCFX_SYSTEM_AEC = System default acoustic echo cancellation.
;                                                                                     - $GUID_DSCFX_SYSTEM_NS  = System default noise suppression.

;                                                      - $aFX[1][2] = Flags that specify desired parameters of the effect.
;                                                                                     - $DSCFX_LOCHARDWARE = Effect specified by guidDSCFXInstance must be in hardware.
;                                                                                     - $DSCFX_LOCSOFTWARE = Effect specified by guidDSCFXInstance must be in software.
;                                                        ...
;                                                      - $aFX[n][0] = sGUID that specifies the class identifier of the n^th effect
;                                                        ...
; Return values .: Success - An IDirectSoundCaptureBuffer object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureCreateCaptureBuffer($oDS_Cap, $iBufferBytes, $iChannels = 2, $iSamplesPerSec = 44100, $iBitsPerSample = 16, $iFormatTag = $WAVE_FORMAT_PCM, $bWaveMapper = False, $aFX = Null)
	If Not IsObj($oDS_Cap) Then Return SetError($DSERR_NOOBJ, 0, False)


	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Local $iAvgBytesPerSec = $iSamplesPerSec * $iBlockAlign

	Local $tWaveFormatEx = DllStructCreate($tagDSWAVEFORMATEX)
	$tWaveFormatEx.FormatTag = $iFormatTag
	$tWaveFormatEx.Channels = $iChannels
	$tWaveFormatEx.SamplesPerSec = $iSamplesPerSec
	$tWaveFormatEx.AvgBytesPerSec = $iAvgBytesPerSec
	$tWaveFormatEx.BlockAlign = $iBlockAlign
	$tWaveFormatEx.BitsPerSample = $iBitsPerSample

	Local $iFlags = 0
	If $bWaveMapper Then $iFlags = $DSCBCAPS_WAVEMAPPED

	Local $tFX, $iFX, $iSize, $tTmp, $iHResult
	Local $tDSC_BufferDesc = DllStructCreate($tagDSCBUFFERDESC)
	If BitAND($iFlags, $DSCBCAPS_CTRLFX) Then
		If UBound($aFX, 0) = 2 And UBound($aFX, 2) > 2 Then
			$iFX = $aFX[0][0]
			If $iFX >= UBound($aFX) Then $iFX = UBound($aFX)
			If $iFX > 0 Then
				$iFlags = BitOR($iFlags, $DSCBCAPS_CTRLFX)
				$tFX = DllStructCreate($tagDSCEFFECTDESC)
				$iSize = DllStructGetSize($tFX)
				$tFX = DllStructCreate("byte[" & $iSize * $iFX & "];")
				$tDSC_BufferDesc.DSCFXDesc = DllStructGetPtr($tFX)
				For $i = 1 To $iFX
					$tTmp = DllStructCreate($tagDSCEFFECTDESC, DllStructGetPtr($tFX) + $tDSC_BufferDesc.FXCount * $iSize)
					$tTmp.Size = $iSize
					$tTmp.Flags = $aFX[$i][2]
					$iHResult = _WinAPI_GUIDFromStringEx($aFX[$i][0], DllStructGetPtr($tTmp, "DSCFXClass"))
					If @error Or $iHResult Then ContinueLoop
					$iHResult = _WinAPI_GUIDFromStringEx($aFX[$i][1], DllStructGetPtr($tTmp, "DSCFXInstance"))
					If @error Or $iHResult Then ContinueLoop
					$tDSC_BufferDesc.FXCount += 1
				Next
			EndIf
		EndIf
	EndIf

	$tDSC_BufferDesc.Size = DllStructGetSize($tDSC_BufferDesc)
	$tDSC_BufferDesc.Flags = $iFlags
	$tDSC_BufferDesc.BufferBytes = $iBufferBytes
	$tDSC_BufferDesc.WaveFormatEX = DllStructGetPtr($tWaveFormatEx)

	Local $pDSC_Buffer
	$iHResult = $oDS_Cap.CreateCaptureBuffer($tDSC_BufferDesc, $pDSC_Buffer, Null)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $oDSC_Buffer = ObjCreateInterface($pDSC_Buffer, $sIID_IDirectSoundCaptureBuffer, $tagIDirectSoundCaptureBuffer8)
	If Not IsObj($oDSC_Buffer) Then Return SetError($DSERR_OBJFAIL, 2, False)

	Return $oDSC_Buffer
EndFunc   ;==>_DSnd_CaptureCreateCaptureBuffer

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureEnumerate
; Description ...: Enumerates the DirectSoundCapture objects installed in the system.
; Syntax ........: _DSnd_CaptureEnumerate()
; Parameters ....:
; Return values .: Success - A two-dimensional array.
;                                $aArray[0][0] = Number of devices
;                                $aArray[1][0] = Description of the first device.
;                                $aArray[1][1] = Module name of the first device.
;                                $aArray[1][2] = GUID of the first device
;                                $aArray[1][0] = Description of the second device.
;                                $aArray[1][1] = Module name of the second device.
;                                $aArray[1][2] = GUID of the second device
;                                ...
;                                $aArray[n][0] = Description of the nth device.
;                                $aArray[n][1] = Module name of the nth device.
;                                $aArray[n][2] = GUID of the nth device
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: GUID can be passed to _DSnd_CaptureCreate to create a device object for that driver.
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureEnumerate()
	Local $hCallBack = DllCallbackRegister("__DSnd_EnumCallback", $DSEnumCallback_Return, $DSEnumCallback_Params)
	If @error Or Not $hCallBack Then SetError($DSERR_UFAIL, 0, False)

	Local $tUserData = DllStructCreate("uint Cnt; ptr GUID[32]; wchar Desc[8192]; wchar Module[8192];")

	_DSnd_CaptureEnumerateEx(DllCallbackGetPtr($hCallBack), $tUserData)
	If @error Then
		DllCallbackFree($hCallBack)
		Return SetError($DSERR_UFAIL, 1, False)
	EndIf

	Local $aRet[$tUserData.Cnt + 1][3] = [[$tUserData.Cnt, "Module", "GUID"]]
	Local $tString
	For $i = 1 To $tUserData.Cnt
		$tString = DllStructCreate("wchar[256];", DllStructGetPtr($tUserData, "Desc") + ($i - 1) * 512)
		$aRet[$i][0] = DllStructGetData($tString, 1)

		$tString = DllStructCreate("wchar[256];", DllStructGetPtr($tUserData, "Module") + ($i - 1) * 512)
		$aRet[$i][1] = DllStructGetData($tString, 1)

		If $tUserData.GUID(($i)) Then $aRet[$i][2] = _WinAPI_StringFromGUID($tUserData.GUID(($i)))
	Next

	Return $aRet
EndFunc   ;==>_DSnd_CaptureEnumerate

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureEnumerateEx
; Description ...: Enumerates the DirectSoundCapture objects installed in the system.
; Syntax ........: _DSnd_CaptureEnumerateEx($pDSEnumCallback[, $tUserData = Null])
; Parameters ....: $pDSEnumCallback     - Pointer of a DSEnumCallback function that will be called for each DirectSoundCapture object installed in the system.
;                  $tUserData           - Pointer of the user-defined context passed to the enumeration callback function every time that function is called.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureEnumerateEx($pDSEnumCallback, $tUserData = Null)
	If Not IsPtr($pDSEnumCallback) Then Return SetError($DSERR_NOPTR, 0, False)

	Local $aResult = DllCall($__g_hDSNDDLL, "uint", "DirectSoundCaptureEnumerate", "struct*", $pDSEnumCallback, "struct*", $tUserData)
	If @error Then Return SetError($DSERR_UFAIL, 1, False)
	If $aResult[0] Then Return SetError($aResult[0], 2, False)

	Return True
EndFunc   ;==>_DSnd_CaptureEnumerateEx

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureGetCaps
; Description ...: Retrieves the capabilities of the capture device.
; Syntax ........: _DSnd_CaptureGetCaps($oDSC)
; Parameters ....: $oDSC                - An IDirectSoundCapture object
; Return values .: Success - A one-dimensional array.
;                                $aCaps[0] = Specifies device capabilities. Can be zero or one or more of the following flags:
;                                                - $DSCCAPS_CERTIFIED       = The driver for the device is a certified WDM driver.
;                                                - $DSCCAPS_EMULDRIVER      = There is no DirectSoundCapture driver for the device, so the standard waveform audio functions are being used.
;                                                - $DSCCAPS_MULTIPLECAPTURE = Multiple capture objects can be used simultaneously on the capture device.
;                                $aCaps[1] = Standard formats that are supported. Can be one or more of the following values:
;                                                - $WAVE_INVALIDFORMAT   = invalid format
;                                                - $WAVE_FORMAT_1M08     = 11.025 kHz, Mono,   8-bit
;                                                - $WAVE_FORMAT_1S08     = 11.025 kHz, Stereo, 8-bit
;                                                - $WAVE_FORMAT_1M16     = 11.025 kHz, Mono,   16-bit
;                                                - $WAVE_FORMAT_1S16     = 11.025 kHz, Stereo, 16-bit
;                                                - $WAVE_FORMAT_2M08     = 22.05  kHz, Mono,   8-bit
;                                                - $WAVE_FORMAT_2S08     = 22.05  kHz, Stereo, 8-bit
;                                                - $WAVE_FORMAT_2M16     = 22.05  kHz, Mono,   16-bit
;                                                - $WAVE_FORMAT_2S16     = 22.05  kHz, Stereo, 16-bit
;                                                - $WAVE_FORMAT_4M08     = 44.1   kHz, Mono,   8-bit
;                                                - $WAVE_FORMAT_4S08     = 44.1   kHz, Stereo, 8-bit
;                                                - $WAVE_FORMAT_4M16     = 44.1   kHz, Mono,   16-bit
;                                                - $WAVE_FORMAT_4S16     = 44.1   kHz, Stereo, 16-bit
;                                                - $WAVE_FORMAT_48M08    = 48     kHz, Mono,   8-bit
;                                                - $WAVE_FORMAT_48S08    = 48     kHz, Stereo, 8-bit
;                                                - $WAVE_FORMAT_48M16    = 48     kHz, Mono,   16-bit
;                                                - $WAVE_FORMAT_48S16    = 48     kHz, Stereo, 16-bit
;                                                - $WAVE_FORMAT_96M08    = 96     kHz, Mono,   8-bit
;                                                - $WAVE_FORMAT_96S08    = 96     kHz, Stereo, 8-bit
;                                                - $WAVE_FORMAT_96M16    = 96     kHz, Mono,   16-bit
;                                                - $WAVE_FORMAT_96S16    = 96     kHz, Stereo, 16-bit
;                                $aCaps[2] = Number of channels supported by the device, where 1 is mono, 2 is stereo, and so on.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureGetCaps($oDSC)
	If Not IsObj($oDSC) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $tDS_Caps = DllStructCreate($tagDSCCAPS)
	$tDS_Caps.Size = DllStructGetSize($tDS_Caps)

	Local $iHResult = $oDSC.GetCaps($tDS_Caps)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $aCaps[3]
	$aCaps[0] = $tDS_Caps.Flags
	$aCaps[1] = $tDS_Caps.Formats
	$aCaps[2] = $tDS_Caps.Channels

	Return $aCaps
EndFunc   ;==>_DSnd_CaptureGetCaps

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureGetCapsFormat
; Description ...: Retrieves a list of standard formats that are supported.
; Syntax ........: _DSnd_CaptureGetCapsFormat($oDSC)
; Parameters ....: $oDSC                - An IDirectSoundCapture object
; Return values .: Success - A two-dimensional array.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureGetCapsFormat($oDSC)
	If Not IsObj($oDSC) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $aCaps = _DSnd_CaptureGetCaps($oDSC)
	If @error Then Return SetError(@error, 1, False)

	Local $aFormat[1], $iFormat = 0

	For $i = 0 To 19
		If BitAND($aCaps[1], 2 ^ $i) Then
			$iFormat += 1
			If $iFormat >= UBound($aFormat) Then ReDim $aFormat[$iFormat * 2]
			If Mod($i, 2) Then
				$aFormat[$iFormat] = "Stereo - "
			Else
				$aFormat[$iFormat] = "Mono - "
			EndIf

			Switch Floor($i / 4)
				Case 0
					$aFormat[$iFormat] &= "11025 Hz - "
				Case 1
					$aFormat[$iFormat] &= "22050 Hz - "
				Case 2
					$aFormat[$iFormat] &= "44100 Hz - "
				Case 3
					$aFormat[$iFormat] &= "48000 Hz - "
				Case 4
					$aFormat[$iFormat] &= "96000 Hz - "
			EndSwitch

			Switch Mod($i, 4)
				Case 0, 1
					$aFormat[$iFormat] &= "8 Bit"
				Case 2, 3
					$aFormat[$iFormat] &= "16 Bit"
			EndSwitch

		EndIf
	Next

	$aFormat[0] = $iFormat
	ReDim $aFormat[$iFormat + 1]
	Return $aFormat
EndFunc   ;==>_DSnd_CaptureGetCapsFormat

; _DSnd_CaptureInitialize

; ###############################################################################################################################
; # DirectSound_CaptureBuffer
; ###############################################################################################################################

; _DSnd_CaptureBufferGetCaps
; _DSnd_CaptureBufferGetCurrentPosition
; _DSnd_CaptureBufferGetFormat
; _DSnd_CaptureBufferGetFXStatus
; _DSnd_CaptureBufferGetObjectInPath
; _DSnd_CaptureBufferGetStatus
; _DSnd_CaptureBufferInitialize

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureBufferLock
; Description ...: Locks a portion of the buffer.
; Syntax ........: _DSnd_CaptureBufferLock($oDSC_Buffer[, $iLockBytes = 0[, $iFlags = $DSCBLOCK_ENTIREBUFFER[, $iOffset = 0]]])
; Parameters ....: $oDSC_Buffer         - This IDirectSoundCaptureBuffer object
;                  $iLockBytes          - Size, in bytes, of the portion of the buffer to lock. Because the buffer is conceptually circular, this number can exceed the number of bytes between dwOffset and the end of the buffer.
;                  $iFlags              - Flags modifying the lock event. This value can be zero or $DSCBLOCK_ENTIREBUFFER = Ignore $iLockBytes and lock the entire capture buffer.
;                  $iOffset             - Offset, in bytes, from the start of the buffer to the point where the lock begins.
; Return values .: Success - A one-dimensional array.
;                                $aLock[0] = Pointer to the first locked part of the buffer.
;                                $aLock[1] = Number of bytes in the block at $aLock[0]. If this value is less than $iBytes, the lock has wrapped and $aLock[2] points to a second block of data at the beginning of the buffer.
;                                $aLock[2] = Pointer to the second locked part of the capture buffer. If NULL is returned, the $aLock[0] parameter points to the entire locked portion of the capture buffer.
;                                $aLock[3] = Number of bytes in the block at $aLock[2]. If $aLock[2] is NULL, this value is zero.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......: _DSnd_BufferLock
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureBufferLock($oDSC_Buffer, $iLockBytes = 0, $iFlags = $DSCBLOCK_ENTIREBUFFER, $iOffset = 0)
	If Not IsObj($oDSC_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $pBuffer, $iSize, $pBuffer2, $iSize2
	Local $iHResult = $oDSC_Buffer.Lock($iOffset, $iLockBytes, $pBuffer, $iSize, $pBuffer2, $iSize2, $iFlags)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $aLock[4]
	$aLock[0] = $pBuffer
	$aLock[1] = $iSize
	$aLock[2] = $pBuffer2
	$aLock[3] = $iSize2
	Return $aLock
EndFunc   ;==>_DSnd_CaptureBufferLock

; _DSnd_CaptureBufferStart
; _DSnd_CaptureBufferStop

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_CaptureBufferUnlock
; Description ...: Unlocks the buffer.
; Syntax ........: _DSnd_CaptureBufferUnlock($oDSC_Buffer, Byref $aLock)
; Parameters ....: $oDSC_Buffer         - This IDirectSoundCaptureBuffer object
;                  $aLock               - Array as returned by _DSnd_CaptureBufferLock
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DSnd_CaptureBufferUnlock($oDSC_Buffer, ByRef $aLock)
	If Not IsObj($oDSC_Buffer) Then Return SetError($DSERR_NOOBJ, 0, False)
	If Not IsArray($aLock) Or UBound($aLock) <> 4 Then Return SetError($DSERR_PARAM, 1, False)

	Local $iHResult = $oDSC_Buffer.Unlock($aLock[0], $aLock[1], $aLock[2], $aLock[3])
	If $iHResult Then Return SetError($iHResult, 2, False)

	$aLock = 0

	Return True
EndFunc   ;==>_DSnd_CaptureBufferUnlock

; _DSnd_CaptureFXAecGetAllParameters
; _DSnd_CaptureFXAecGetStatus
; _DSnd_CaptureFXAecReset
; _DSnd_CaptureFXAecSetAllParameters
; _DSnd_CaptureFXNoiseSuppressGetAllParameters
; _DSnd_CaptureFXNoiseSuppressReset
; _DSnd_CaptureFXNoiseSuppressSetAllParameters

; ###############################################################################################################################
; # DirectSound_FX
; ###############################################################################################################################
; _DSnd_FXI3DL2ReverbGetAllParameters
; _DSnd_FXI3DL2ReverbGetPreset
; _DSnd_FXI3DL2ReverbGetQuality
; _DSnd_FXI3DL2ReverbSetAllParameters
; _DSnd_FXI3DL2ReverbSetPreset
; _DSnd_FXI3DL2ReverbSetQuality

; ###############################################################################################################################
; # DirectSound_3D
; ###############################################################################################################################
; _DSnd_3DBufferGetAllParameters
; _DSnd_3DBufferGetConeAngles
; _DSnd_3DBufferGetConeOrientation
; _DSnd_3DBufferGetConeOutsideVolume
; _DSnd_3DBufferGetMaxDistance
; _DSnd_3DBufferGetMinDistance
; _DSnd_3DBufferGetMode
; _DSnd_3DBufferGetPosition
; _DSnd_3DBufferGetVelocity
; _DSnd_3DBufferSetAllParameters
; _DSnd_3DBufferSetConeAngles
; _DSnd_3DBufferSetConeOrientation
; _DSnd_3DBufferSetConeOutsideVolume
; _DSnd_3DBufferSetMaxDistance
; _DSnd_3DBufferSetMinDistance
; _DSnd_3DBufferSetMode
; _DSnd_3DBufferSetPosition
; _DSnd_3DBufferSetVelocity

; _DSnd_3DListenerCommitDeferredSettings
; _DSnd_3DListenerGetAllParameters
; _DSnd_3DListenerGetDistanceFactor
; _DSnd_3DListenerGetDopplerFactor
; _DSnd_3DListenerGetOrientation
; _DSnd_3DListenerGetPosition
; _DSnd_3DListenerGetRolloffFactor
; _DSnd_3DListenerGetVelocity
; _DSnd_3DListenerSetAllParameters
; _DSnd_3DListenerSetDistanceFactor
; _DSnd_3DListenerSetDopplerFactor
; _DSnd_3DListenerSetOrientation
; _DSnd_3DListenerSetPosition
; _DSnd_3DListenerSetRolloffFactor
; _DSnd_3DListenerSetVelocity

; ###############################################################################################################################
; # DirectSound Property
; ###############################################################################################################################
; IKsPropertySetGet
; IKsPropertySetQuerySupport
; IKsPropertySetSet

; #FUNCTION# ====================================================================================================================
; Name ..........: _DSnd_WaveLoadFile
; Description ...: Loads a WAV file from harddisk
; Syntax ........: _DSnd_WaveLoadFile($sFile)
; Parameters ....: $sFile               - path of the wavfile.
; Return values .: Success - An Array and sets @extended to 1 if FormatTag is not $WAVE_FORMAT_PCM
;                                - $aWav[0] = BinaryData of wavfile
;                                - $aWav[1] = WAVEFORMATEX struct
;                                       - $aWav[1].FormatTag      = Waveform audio format type.
;                                       - $aWav[1].Channels       = Number of channels of audio data.
;                                       - $aWav[1].SamplesPerSec  = Sample frequency (samples per second) at which each channel should be played.
;                                       - $aWav[1].AvgBytesPerSec = Average data transfer rate in bytes per second
;                                       - $aWav[1].BlockAlign     = Size of the minimum atomic unit of data for the FormatTag format type. If FormatTag = $WAVE_FORMAT_PCM or $WAVE_FORMAT_IEEE_FLOAT then BlockAlign = Channels * (BitsPerSample / 8)
;                                       - $aWav[1].BitsPerSample  = Number of bits per sample. (must be a multiple of 8)
;                                       - $aWav[1].Size           = Size, in bytes, of extra format information
;                                       - [$aWav[1].ExtraData]    = [optional] This information is used by non-PCM formats to store extra attributes for the FormatTag.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _DSnd_WaveLoadFromFile($sFile)
	Local $hFile = FileOpen($sFile, 16)
	Local $bWav = FileRead($hFile)
	If @error Then Return SetError($DSERR_PARAM, 0, False)
	FileClose($hFile)

	Local $tWav = DllStructCreate("byte[" & BinaryLen($bWav) & "];")
	Local $pWav = DllStructGetPtr($tWav)
	DllStructSetData($tWav, 1, $bWav)

	Local $tChunk = DllStructCreate("char RIFF[4]; uint FileSize; char WAVE[4]; byte Offset;", $pWav)
	If $tChunk.RIFF <> "RIFF" Or $tChunk.WAVE <> "WAVE" Then Return SetError($DSERR_PARAM, 1, False) ;Not a WaveFile
	Local $iFileSize = $tChunk.FileSize
	If $iFileSize > DllStructGetSize($tWav) Then Return SetError($DSERR_UFAIL, 2, False)

	Local $iOffset = DllStructGetPtr($tChunk, "Offset") - $pWav
	Local $iDone = 0, $iDataOffset, $iDataLen, $iFMTOffset, $iFMTLen

	While $iOffset < $iFileSize ;Find FMT_ and DATA Chunks
		$tChunk = DllStructCreate("char Name[4]; uint Len;", $pWav + $iOffset)
		Switch $tChunk.Name
			Case "FMT "
				$iDone += 1
				$iFMTOffset = $iOffset
				$iFMTLen = $tChunk.Len
			Case "DATA"
				$iDone += 1
				$iDataOffset = $iOffset
				$iDataLen = $tChunk.Len
		EndSwitch
		$iOffset += $tChunk.Len + 8
		If $iDone > 1 Then ExitLoop
	WEnd

	If $iDone < 2 Or Not $iDataOffset Or Not $iFMTOffset Then Return SetError($DSERR_UFAIL, 3, False)

	$tChunk = DllStructCreate("char Name[4]; uint Len; byte[" & $iDataLen & "];", $pWav + $iDataOffset)
	Local $bData = DllStructGetData($tChunk, 3)

	If $iFMTLen < 16 Then Return SetError($DSERR_UFAIL, 4, False) ;No PCMWaveFormat
	$tChunk = DllStructCreate($tagDSWAVEFORMATEX, $pWav + $iFMTOffset + 8)
	Local $tWaveFormatEx
	Switch $iFMTLen
		Case 16, 18 ;PCMWaveFormat, WaveFormatEX
			$tWaveFormatEx = DllStructCreate($tagDSWAVEFORMATEX)
		Case Else
			$tWaveFormatEx = DllStructCreate($tagDSWAVEFORMATEX & " byte ExtraData[" & $tChunk.Size & "];")
			$tWaveFormatEx.Size = $tChunk.Size
			$tWaveFormatEx.ExtraData = DllStructGetData(DllStructCreate("byte[" & $tChunk.Size & "];", DllStructGetPtr($tChunk, "Size")), 1)
	EndSwitch

	$tWaveFormatEx.FormatTag = $tChunk.FormatTag
	$tWaveFormatEx.Channels = $tChunk.Channels
	$tWaveFormatEx.SamplesPerSec = $tChunk.SamplesPerSec
	$tWaveFormatEx.AvgBytesPerSec = $tChunk.AvgBytesPerSec
	$tWaveFormatEx.BlockAlign = $tChunk.BlockAlign
	$tWaveFormatEx.BitsPerSample = $tChunk.BitsPerSample

	Local $aRet[2]
	$aRet[0] = $bData
	$aRet[1] = $tWaveFormatEx
	Return SetExtended($tWaveFormatEx.FormatTag <> $WAVE_FORMAT_PCM, $aRet) ;Extended = 1 if not PCM
EndFunc   ;==>_DSnd_WaveLoadFromFile

Func _DSnd_MP3Decode(ByRef $bMP3, $iChannels = 0, $iBitsPerSample = 16, $iFormatTag = $WAVE_FORMAT_PCM, $sSubFormat = $sMEDIASUBTYPE_PCM)
	If Not IsBinary($bMP3) Then Return SetError($DSERR_PARAM, 0, False)

	Local $iOffset = 1, $iHeader, $iFlags
	;Skip ID3v2
	Local $aRegExp = StringRegExp(BinaryMid($bMP3, 1, 128), "494433.{14}", 1)
	If Not @error Then
		$iOffset = @extended - 22
		If Mod($iOffset, 2) Then ;Byte Boundaries
			$iHeader = Dec(StringRight($aRegExp[0], 8))
			If BitAND($iHeader, 0xFF) <= 0x7F And BitAND(BitShift($iHeader, 8), 0xFF) <= 0x7F And BitAND(BitShift($iHeader, 16), 0xFF) <= 0x7F And BitAND(BitShift($iHeader, 24), 0xFF) <= 0x7F Then ;Size Bytes <= 7F!
				If BitAND(BitShift($iHeader, 40), 0xFF) <> 0xFF And BitAND(BitShift($iHeader, 48), 0xFF) <> 0xFF Then ;Version Bytes <> FF!
					$iFlags = Dec(StringLeft(StringTrimLeft($aRegExp[0], 10), 2))
					$iOffset += BitOR(BitAND($iHeader, 0x7F), BitShift(BitAND($iHeader, 0x7F00), 1), BitShift(BitAND($iHeader, 0x7F0000), 2), BitShift(BitAND($iHeader, 0x7F000000), 3)) + (10 * BitShift(BitAND($iFlags, 0x8), 3)) + 10
					If Not StringRegExp(BinaryMid($bMP3, $iOffset, 2), "(?mi)^0x(FFF|FFE)") Then ;Offset does not match frameheader
						$aRegExp = StringRegExp(BinaryMid($bMP3, 1, 2 ^ 28 + 20 + 1441), "FF[FE][23AB].{4}", 1) ;Match Header Mpeg(1/2/2.5) - Layer_III - ProtectionBit ON/OFF
						If Not @error Then
							$iOffset = @extended
						Else
							Return SetError($DSERR_UFAIL, 1, False) ;No MP3 FrameHeader found
						EndIf
					EndIf
				EndIf
			EndIf
		EndIf
	EndIf

	;FrameHeader
	$aRegExp = StringRegExp(BinaryMid($bMP3, $iOffset, 1441), "FF[FE][23AB].{4}", 1) ;Match Header Mpeg(1/2/2.5) - Layer_III - ProtectionBit ON/OFF
	If @error Then Return SetError($DSERR_UFAIL, 2, False)
	$iOffset += @extended - 11
	If Not Mod(@extended, 2) Then Return SetError($DSERR_UFAIL, 3, False) ;Byte Boundaries ([0x.. FF FB ..] vs [0x.. 1F FF B1 ..])

	$iHeader = Dec($aRegExp[0])
	Local $iID = BitAND(BitShift($iHeader, 19), 0x3)
	If $iID = 1 Then Return SetError($DSERR_UFAIL, 4, False) ;Not Mpeg 1/2/2.5
	If BitAND(BitShift($iHeader, 17), 0x3) <> 1 Then Return SetError($DSERR_UFAIL, 5, False) ; <> Layer III

	Local $iBitRate = BitAND(BitShift($iHeader, 12), 0xF), $iBRM, $fBRF
	Switch $iID
		Case 3 ;MPEG Version 1
			$iBRM = Mod($iBitRate + 2, 4) + 1
			$fBRF = 2 ^ BitShift($iBitRate + 2, 2)
			$iBitRate = 16 * $fBRF + 4 * $fBRF * $iBRM
		Case Else ;MPEG Version 2.5 + MPEG Version 2
			$iBRM = Mod($iBitRate - 1, 8) + 1
			$fBRF = 2 ^ BitShift($iBitRate - 1, 3)
			$iBitRate = 8 * $fBRF * $iBRM + 64 * ($fBRF - 1)
	EndSwitch

	Local $iSampleRate
	Switch BitAND(BitShift($iHeader, 10), 0x3)
		Case 0
			$iSampleRate = 11025
		Case 1
			$iSampleRate = 12000
		Case 2
			$iSampleRate = 8000
		Case Else ; 3 = reserved
			Return SetError($DSERR_UFAIL, 6, False)
	EndSwitch
	Switch $iID
		Case 2
			$iSampleRate *= 2 ;Mpeg2 = 16000, 24000, 22050
		Case 3
			$iSampleRate *= 4 ;Mpeg1 = 32000, 48000, 44100
	EndSwitch

	If $iChannels = 0 Then $iChannels = (BitAND(BitShift($iHeader, 6), 0x3) <> 3) + 1 ;0,1=Stereo 2=DualMono 3=Mono

	Local $fDuration = BinaryLen($bMP3) / $iBitRate * 0.008

	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Local $iBufferSize = Ceiling($fDuration * $iSampleRate * $iBlockAlign)
	If $iBufferSize < 1764000 Then ;10 Sec if 44100 Stereo 16 Bit
		$iBufferSize = 1764000
	ElseIf $iBufferSize > 105840000 Then ;10 Min if 44100 Stereo 16 Bit
		$iBufferSize = 105840000
	EndIf

	Local $oDMO = _DMO_CreateInstance($sCLSID_CMP3DecMediaObject, $sIID_IMediaObject, $tagIMediaObject)
	If @error Then Return SetError(@error, 7, False)

	If Not _DMO_MediaObjectSetInputType($oDMO, $iChannels, $iSampleRate, 0, $WAVE_FORMAT_MPEGLAYER3, $sMEDIASUBTYPE_MP3) Then Return SetError(@error, 8, False)
	If Not _DMO_MediaObjectSetOutputType($oDMO, $iChannels, $iSampleRate, $iBitsPerSample, $iFormatTag, $sSubFormat) Then Return SetError(@error, 9, False)

	Local $iHResult
	Local $tBufferIn = DllStructCreate("byte[" & BinaryLen($bMP3) & "];")
	DllStructSetData($tBufferIn, 1, $bMP3)
	Local $oIMB_In = _DMO_MediaBufferCreate($tBufferIn)
	If @error Then Return SetError(@error, 3, False)
	$oIMB_In.SetLength(BinaryLen($bMP3))
	$iHResult = $oDMO.ProcessInput(0, $oIMB_In, 0, 0, 0)
	If $iHResult Then
		_DMO_MediaBufferDispose($oIMB_In)
		Return SetError($iHResult, 10, False)
	EndIf

	Local $tBufferOut = DllStructCreate("byte[" & $iBufferSize & "];")
	Local $oIMB_Out = _DMO_MediaBufferCreate($tBufferOut)
	If @error Then Return SetError(@error, 5, False)

	Local $tOutBuffers = DllStructCreate($tagDMO_OUTPUT_DATA_BUFFER)
	DllStructSetData($tOutBuffers, 1, DllStructGetData(DllStructCreate("ptr[5]; ptr;", $oIMB_Out), 2))

	Local $pBuffer, $iBytes, $bPCM = BinaryMid(0, 1, 0), $tGet
	While 1
		$oIMB_Out.SetLength(0)
		$iHResult = $oDMO.ProcessOutput(0, 1, $tOutBuffers, 0)
		If $iHResult Then ExitLoop

		$oIMB_Out.GetBufferAndLength($pBuffer, $iBytes)
		$tGet = DllStructCreate("byte[" & $iBytes & "];", $pBuffer)
		$bPCM &= DllStructGetData($tGet, 1)

		If Not BitAND($tOutBuffers.Status, $DMO_OUTPUT_DATA_BUFFERF_INCOMPLETE) Then ExitLoop
	WEnd

	$oDMO = 0
	_DMO_MediaBufferDispose($oIMB_In)
	_DMO_MediaBufferDispose($oIMB_Out)

	If BinaryLen($bPCM) < 1 Then Return SetError($DSERR_PARAM, 6, False)

	Local $iAvgBytesPerSec = $iSampleRate * $iBlockAlign

	Local $aWav[2]
	$aWav[0] = $bPCM
	$aWav[1] = DllStructCreate($tagDSWAVEFORMATEX)
	$aWav[1].FormatTag = $iFormatTag
	$aWav[1].Channels = $iChannels
	$aWav[1].SamplesPerSec = $iSampleRate
	$aWav[1].AvgBytesPerSec = $iAvgBytesPerSec
	$aWav[1].BlockAlign = $iBlockAlign
	$aWav[1].BitsPerSample = $iBitsPerSample

	Return $aWav
EndFunc   ;==>_DSnd_MP3Decode

Func _DSnd_Seconds2Bytes($fSec, $iChannels = 2, $iSampleRate = 44100, $iBitsPerSample = 16)
	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Return Ceiling($fSec * $iSampleRate * $iBlockAlign)
EndFunc   ;==>_DSnd_Seconds2Bytes

Func _DSnd_Bytes2Seconds($iBytes, $iChannels = 2, $iSampleRate = 44100, $iBitsPerSample = 16)
	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Return $iBytes / $iSampleRate / $iBlockAlign
EndFunc   ;==>_DSnd_Bytes2Seconds

Func _DSnd_Seconds2Time($fSec)
	Local $iH = Int($fSec / 3600)
	$fSec = Mod($fSec, 3600)
	Local $iM = Int($fSec / 60)
	$fSec = Mod($fSec, 60)
	Local $iS = Floor($fSec)
	Local $iMS = Floor(($fSec - $iS) * 1000)
	Return StringFormat("%02s:%02s:%02s.%03s", $iH, $iM, $iS, $iMS)
EndFunc   ;==>_DSnd_Seconds2Time

Func _DSnd_Time2Seconds($sTime)
	Local $aRegExp = StringRegExp($sTime, "(\d+):(\d{2}):(\d{2}).(\d{3})", 3)
	If @error Then Return SetError(1, 0, False)
	Return Number($aRegExp[0]) * 3600 + Number($aRegExp[1]) * 60 + Number($aRegExp[2]) + Number($aRegExp[3]) * 0.001
EndFunc   ;==>_DSnd_Time2Seconds

#cs
	HRESULT GetDeviceID(
	LPCGUID pGuidSrc,
	LPGUID pGuidDest
#ce

Func _DSnd_SPEAKERCOMBINED($iC, $iG)
	Return BitOR(BitAND(Int($iC), 0xFF), BitShift(BitAND(Int($iG), 0xFF), -16))
EndFunc   ;==>_DSnd_SPEAKERCOMBINED

Func _DSnd_SPEAKERCONFIG($iA)
	Return BitAND(Int($iA), 0xFF)
EndFunc   ;==>_DSnd_SPEAKERCONFIG

Func _DSnd_SPEAKERGEOMETRY($iA)
	Return BitAND(BitShift(Int($iA), 16), 0xFF)
EndFunc   ;==>_DSnd_SPEAKERGEOMETRY

; ###############################################################################################################################
; # Debug
; ###############################################################################################################################
Func _DSnd_ErrorMessage($iHResult, $iCurError = @error, $iCurExtended = @extended)
	Local $sErr
	Switch $iHResult
		Case $DS_OK
			$sErr = "OK."
		Case $DS_NO_VIRTUALIZATION
			$sErr = "$DS_NO_VIRTUALIZATION - The call succeeded, but we had to substitute the 3D algorithm."
		Case $DSERR_ALLOCATED
			$sErr = "$DSERR_ALLOCATED - The call failed because resources (such as a priority level) were already being used by another caller."
		Case $DSERR_CONTROLUNAVAIL
			$sErr = "$DSERR_CONTROLUNAVAIL - The control (vol, pan, etc.) requested by the caller is not available."
		Case $DSERR_INVALIDPARAM
			$sErr = "$DSERR_INVALIDPARAM - An invalid parameter was passed to the returning function."
		Case $DSERR_INVALIDCALL
			$sErr = "$DSERR_INVALIDCALL - This call is not valid for the current state of this object."
		Case $DSERR_GENERIC
			$sErr = "$DSERR_GENERIC - An undetermined error occurred inside the DirectSound subsystem."
		Case $DSERR_PRIOLEVELNEEDED
			$sErr = "$DSERR_PRIOLEVELNEEDED - The caller does not have the priority level required for the function to succeed."
		Case $DSERR_OUTOFMEMORY
			$sErr = "$DSERR_OUTOFMEMORY - Not enough free memory is available to complete the operation."
		Case $DSERR_BADFORMAT
			$sErr = "$DSERR_BADFORMAT - The specified WAVE format is not supported."
		Case $DSERR_UNSUPPORTED
			$sErr = "$DSERR_UNSUPPORTED - The function called is not supported at this time."
		Case $DSERR_NODRIVER
			$sErr = "$DSERR_NODRIVER - No sound driver is available for use."
		Case $DSERR_ALREADYINITIALIZED
			$sErr = "$DSERR_ALREADYINITIALIZED - This object is already initialized."
		Case $DSERR_NOAGGREGATION
			$sErr = "$DSERR_NOAGGREGATION - This object does not support aggregation."
		Case $DSERR_BUFFERLOST
			$sErr = "$DSERR_BUFFERLOST - The buffer memory has been lost, and must be restored."
		Case $DSERR_OTHERAPPHASPRIO
			$sErr = "$DSERR_OTHERAPPHASPRIO - Another app has a higher priority level, preventing this call from succeeding."
		Case $DSERR_UNINITIALIZED
			$sErr = "$DSERR_UNINITIALIZED - This object has not been initialized."
		Case $DSERR_NOINTERFACE
			$sErr = "$DSERR_NOINTERFACE - The requested COM interface is not available."
		Case $DSERR_ACCESSDENIED
			$sErr = "$DSERR_ACCESSDENIED - Access is denied."
		Case $DSERR_BUFFERTOOSMALL
			$sErr = "$DSERR_BUFFERTOOSMALL - Tried to create a $DSBCAPS_CTRLFX buffer shorter than $DSBSIZE_FX_MIN milliseconds."
		Case $DSERR_DS8_REQUIRED
			$sErr = "$DSERR_DS8_REQUIRED - Attempt to use DirectSound 8 functionality on an older DirectSound object."
		Case $DSERR_SENDLOOP
			$sErr = "$DSERR_SENDLOOP - A circular loop of send effects was detected."
		Case $DSERR_BADSENDBUFFERGUID
			$sErr = "$DSERR_BADSENDBUFFERGUID - The GUID specified in an audiopath file does not match a valid MIXIN buffer."
		Case $DSERR_OBJECTNOTFOUND
			$sErr = "$DSERR_OBJECTNOTFOUND - The object requested was not found (numerically equal to $DMUS_E_NOT_FOUND)."
		Case $DSERR_FXUNAVAILABLE
			$sErr = "$DSERR_FXUNAVAILABLE - The effects requested could not be found on the system, or they were found but in the wrong order, or in the wrong hardware/software locations."
		Case Else
			Local $tBufferPtr = DllStructCreate("ptr")

			Local $nCount = _WinAPI_FormatMessage(BitOR($FORMAT_MESSAGE_ALLOCATE_BUFFER, $FORMAT_MESSAGE_FROM_SYSTEM), 0, $iHResult, 0, $tBufferPtr, 0, 0)
			If @error Then Return SetError($iCurError, $iCurExtended, "0x" & Hex($iHResult, 8) & " - Unknown error...")

			Local $sText = ""
			Local $pBuffer = DllStructGetData($tBufferPtr, 1)
			If $pBuffer Then
				If $nCount > 0 Then
					Local $tBuffer = DllStructCreate("wchar[" & ($nCount + 1) & "]", $pBuffer)
					$sText = DllStructGetData($tBuffer, 1)
				EndIf
				_WinAPI_LocalFree($pBuffer)
			EndIf

			$sErr = "0x" & Hex($iHResult) & " - " & $sText
	EndSwitch

	$sErr = StringRegExpReplace($sErr, "(?m)\R$", "")

	Return SetError($iCurError, $iCurExtended, $sErr) ; restore caller @error and @extended
EndFunc   ;==>_DSnd_ErrorMessage

; ###############################################################################################################################
; # DirectSound Internal
; ###############################################################################################################################

Func __DSnd_EnumCallback($pGUID, $sDescription, $sModule, $pUserData)
	If Not $pUserData Then Return False
	Local $tUserData = DllStructCreate("uint Cnt; ptr GUID[32]; wchar Desc[8192]; wchar Module[8192];", $pUserData)
	If Not IsDllStruct($tUserData) Then Return False

	If $tUserData.Cnt + 1 > 32 Then Return False ;Max 32 Devices
	$tUserData.Cnt += 1

	$tUserData.GUID(($tUserData.Cnt)) = $pGUID

	Local $tString = DllStructCreate("wchar[256];", DllStructGetPtr($tUserData, "Desc") + ($tUserData.Cnt - 1) * 512)
	DllStructSetData($tString, 1, $sDescription)

	$tString = DllStructCreate("wchar[256];", DllStructGetPtr($tUserData, "Module") + ($tUserData.Cnt - 1) * 512)
	DllStructSetData($tString, 1, $sModule)

	Return True
EndFunc   ;==>__DSnd_EnumCallback

; ###############################################################################################################################
; # DirectX MediaObject Functions
; ###############################################################################################################################

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_CreateInstance
; Description ...: Creates a DirectX Media Object.
; Syntax ........: _DMO_CreateInstance($sCLSID, $sIID, $tagIntF)
; Parameters ....: $sCLSID              - CLSID of the first DMO.
;                  $sIID                - String representation of interface identifier.
;                  $tagIntF             - String describing v-table of the object.
; Return values .: Success - A DMO object.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_CreateInstance($sCLSID, $sIID, $tagIntF)
	Local $tCLSID = _WinAPI_GUIDFromString($sCLSID)
	If @error Then Return SetError($DSERR_PARAM, 0, False)
	Local $tIID = _WinAPI_GUIDFromString($sIID)
	If @error Then Return SetError($DSERR_PARAM, 1, False)

	Local $aResult = DllCall("Ole32.dll", "uint", "CoCreateInstance", "struct*", $tCLSID, "struct*", Null, "uint", $CLSCTX_INPROC_SERVER, "struct*", $tIID, "ptr*", 0)
	If @error Then Return SetError($DSERR_UFAIL, 2, False)
	If $aResult[0] Then Return SetError($aResult[0], 3, False)

	Local $oDMO = ObjCreateInterface($aResult[5], $sIID, $tagIntF)
	If Not IsObj($oDMO) Then Return SetError($DSERR_OBJFAIL, 4, False)

	Return $oDMO
EndFunc   ;==>_DMO_CreateInstance

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_QueryInterface
; Description ...: Retrieves pointers to the supported interfaces on an object.
; Syntax ........: _DMO_QueryInterface($oDMO, $sIID, $tagIntF)
; Parameters ....: $oDMO                - A DMO object.
;                  $sIID                - String representation of interface identifier.
;                  $tagIntF             - String describing v-table of the object.
; Return values .: Success - A DMO object.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_QueryInterface($oDMO, $sIID, $tagIntF)
	If Not IsObj($oDMO) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $tIID = _WinAPI_GUIDFromString($sIID)

	Local $pObj
	Local $iHResult = $oDMO.QueryInterface($tIID, $pObj)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Local $oObj = ObjCreateInterface($pObj, $sIID, $tagIntF)
	If Not IsObj($oObj) Then Return SetError($DSERR_OBJFAIL, 2, False)

	Return $oObj
EndFunc   ;==>_DMO_QueryInterface

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_Enum
; Description ...: Enumerates DMOs listed in the registry.
; Syntax ........: _DMO_Enum($sGUIDCategory[, $iFlags = 1])
; Parameters ....: $sGUIDCategory       - GUID that specifies which category of DMO to search. Use GUID_NULL to search every category.
;                  $iFlags              - 0 or $DMO_ENUMF_INCLUDE_KEYED - The enumeration should include DMOs whose use is restricted by a software key. If this flag is absent, keyed DMOs are omitted from the enumeration.
; Return values .: Success - A two-dimensional array.
;                                $aArray[0][0] = Number of devices
;                                $aArray[1][0] = CLSID of the first DMO.
;                                $aArray[1][1] = Friendly name of the first DMO.
;                                $aArray[2][0] = CLSID of the second DMO.
;                                $aArray[2][1] = Friendly name of the second DMO.
;                                ...
;                                $aArray[n][0] = CLSID of the nth DMO.
;                                $aArray[n][1] = Friendly name of the nth DMO.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......: _DMO_CreateInstance
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_Enum($sGUIDCategory, $iFlags = 1)
	Local $aList[1][2] = [[0]], $iCnt = 0

	;Enumerate all the DMOs registered as $sGUIDCategory
	Local $oEnum = _DMO_EnumEx($sGUIDCategory, $iFlags)
	If @error Then Return SetError(1, 0, $aList)

	;Get information about the next DMO in the enumeration
	Local $tCLSID = DllStructCreate("byte[16];")
	Local $iReturned, $pDMOName, $tDMOName, $aResult
	While 1
		If $oEnum.Next(1, $tCLSID, $pDMOName, $iReturned) Then ExitLoop

		If $iReturned = 1 Then
			$iCnt += 1
			If $iCnt >= UBound($aList) Then ReDim $aList[$iCnt * 2][2]

			$aResult = DllCall("kernel32.dll", "int", "lstrlenW", "ptr", $pDMOName)
			$tDMOName = DllStructCreate("wchar[" & $aResult[0] & "]", $pDMOName)

			$aList[$iCnt][0] = _WinAPI_StringFromGUID($tCLSID)
			$aList[$iCnt][1] = DllStructGetData($tDMOName, 1)
		EndIf

		_WinAPI_CoTaskMemFree($pDMOName)
	WEnd

	ReDim $aList[$iCnt + 1][2]
	$aList[0][0] = $iCnt

	Return $aList
EndFunc   ;==>_DMO_Enum

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_EnumEx
; Description ...: Enumerates DMOs listed in the registry.
; Syntax ........: _DMO_EnumEx($sGUIDCategory, $iFlags[, $iInTypes = 0[, $tInTypes = Null[, $iOutTypes = 0[, $tOutTypes = Null]]]])
; Parameters ....: $sGUIDCategory       - GUID that specifies which category of DMO to search. Use GUID_NULL to search every category.
;                  $iFlags              - Bitwise combination of zero or more flags from the DMO_ENUM_FLAGS enumeration.
;                  $iInTypes            - Number of input media types to use in the search criteria. Use zero to match any input type.
;                  $tInTypes            - Pointer to an array of $tagDMO_PARTIAL_MEDIATYPE structures that contain the input media types.
;                  $iOutTypes           - Number of output media types to use in the search criteria. Use zero to match any output type.
;                  $tOutTypes           - Pointer to an array of $tagDMO_PARTIAL_MEDIATYPE structures that contain the output media types.
; Return values .: Success - An IEnumDMO object
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_EnumEx($sGUIDCategory, $iFlags, $iInTypes = 0, $tInTypes = Null, $iOutTypes = 0, $tOutTypes = Null)
	Local $tGUIDCategory = _WinAPI_GUIDFromString($sGUIDCategory)

	Local $aResult = DllCall($__g_hMsdmoDLL, "uint", "DMOEnum", "struct*", $tGUIDCategory, "uint", $iFlags, "uint", $iInTypes, "struct*", $tInTypes, "uint", $iOutTypes, "struct*", $tOutTypes, "ptr*", 0)
	If @error Then Return SetError(@error, @extended, False)
	If $aResult[0] Then Return SetError($aResult[0], 1, False)

	Local $oEnum = ObjCreateInterface($aResult[7], $sIID_IEnumDMO, $tagIEnumDMO)
	If Not IsObj($oEnum) Then Return SetError(1, 2, False)

	Return $oEnum
EndFunc   ;==>_DMO_EnumEx

; _DMO_GetName
; _DMO_GetTypes
; _DMO_Register
; _DMO_Unregister
; _DMO_MediaTypeCopy
; _DMO_MediaTypeCreate
; _DMO_MediaTypeDelete
; _DMO_MediaTypeDuplicate

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaTypeFree
; Description ...: Frees the allocated members of a media type structure.
; Syntax ........: _DMO_MediaTypeFree($pDMO_MEDIA_TYPE)
; Parameters ....: $pDMO_MEDIA_TYPE     - Pointer to an initialized $tagDMO_MEDIA_TYPE structure.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaTypeFree($pDMO_MEDIA_TYPE)
	Local $aResult = DllCall($__g_hMsdmoDLL, "uint", "MoFreeMediaType", "struct*", $pDMO_MEDIA_TYPE)
	If @error Then Return SetError(@error, @extended, False)
	If $aResult[0] Then Return SetError($aResult[0], 1, False)
	Return True
EndFunc   ;==>_DMO_MediaTypeFree

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaTypeInit
; Description ...: Initializes a media type structure.
; Syntax ........: _DMO_MediaTypeInit($tStruct)
; Parameters ....: $tStruct             - Pointer to an uninitialized $tagDMO_MEDIA_TYPE structure allocated by the caller.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaTypeInit($tStruct)
	Local $aResult = DllCall($__g_hMsdmoDLL, "uint", "MoInitMediaType", "struct*", $tStruct, "uint", DllStructGetSize($tStruct))
	If @error Then Return SetError(@error, @extended, False)
	If $aResult[0] Then Return SetError($aResult[0], 1, False)
	Return True
EndFunc   ;==>_DMO_MediaTypeInit

; ###############################################################################################################################
; # DMO Enum
; ###############################################################################################################################

; _DMO_EnumClone
; _DMO_EnumNext
; _DMO_EnumReset
; _DMO_EnumSkip

; ###############################################################################################################################
; # DMO MediaBuffer
; ###############################################################################################################################

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaBufferDispose
; Description ...: Frees an IMediaBuffer object.
; Syntax ........: _DMO_MediaBufferDispose(Byref $oSelf)
; Parameters ....: $oSelf               - The IMediaBuffer object created by _DMO_MediaBufferCreate
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaBufferDispose(ByRef $oSelf)
	If Not IsObj($oSelf) Then Return SetError(1, 0, False)
	Local $pPtr = DllStructGetData(DllStructCreate("ptr[5]; ptr;", $oSelf), 2)
	Local $tSelf = DllStructCreate($tagDMO_MEDIABUFFER, $pPtr)

	$oSelf = 0

	If $tSelf.IsFree Then
		_MemVirtualFree($tSelf.pQueryInterface, 0, $MEM_RELEASE)
		_MemVirtualFree($tSelf.pAddRef, 0, $MEM_RELEASE)
		_MemVirtualFree($tSelf.pRelease, 0, $MEM_RELEASE)
		_MemVirtualFree($tSelf.pSetLength, 0, $MEM_RELEASE)
		_MemVirtualFree($tSelf.pGetMaxLength, 0, $MEM_RELEASE)
		_MemVirtualFree($tSelf.pGetBufferAndLength, 0, $MEM_RELEASE)

		$tSelf = 0
		_MemVirtualFree($pPtr, 0, $MEM_RELEASE)
	Else
		Return SetError(1, 1, False)
	EndIf

	Return True
EndFunc   ;==>_DMO_MediaBufferDispose

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaBufferCreate
; Description ...: Creates an IMediaBuffer interface for manipulating a data buffer.
; Syntax ........: _DMO_MediaBufferCreate($tBuffer)
; Parameters ....: $tBuffer             - A DllStructCreate data buffer.
; Return values .: Success - An IMediaBuffer object.
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......: Buffers passed to the IMediaObject.ProcessInput and ProcessOutput methods must implement this interface.
; Related .......: _DMO_MediaBufferDispose
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaBufferCreate($tBuffer)
	Local $pSelf = _MemVirtualAlloc(0, 128, $MEM_COMMIT, $PAGE_READWRITE)
	Local $tSelf = DllStructCreate($tagDMO_MEDIABUFFER, $pSelf)
	$tSelf.Vtbl = DllStructGetPtr($tSelf, "QueryInterface")

	Local $pPtr
	Switch @AutoItX64
		Case 0
			$tSelf.QueryInterface = __DSnd_ASMCreate($bDMO_MBQueryInterfaceASM, $pPtr)
			$tSelf.pQueryInterface = $pPtr
			$tSelf.AddRef = __DSnd_ASMCreate($bDMO_MBAddRefASM, $pPtr)
			$tSelf.pAddRef = $pPtr
			$tSelf.Release = __DSnd_ASMCreate($bDMO_MBReleaseASM, $pPtr)
			$tSelf.pRelease = $pPtr
			$tSelf.SetLength = __DSnd_ASMCreate($bDMO_MBSetLengthASM, $pPtr)
			$tSelf.pSetLength = $pPtr
			$tSelf.GetMaxLength = __DSnd_ASMCreate($bDMO_MBGetMaxLengthASM, $pPtr)
			$tSelf.pGetMaxLength = $pPtr
			$tSelf.GetBufferAndLength = __DSnd_ASMCreate($bDMO_MBGetBufferAndLengthASM, $pPtr)
			$tSelf.pGetBufferAndLength = $pPtr
		Case Else
			$tSelf.QueryInterface = __DSnd_ASMCreate($bDMO_MBQueryInterfaceASM64, $pPtr)
			$tSelf.pQueryInterface = $pPtr
			$tSelf.AddRef = __DSnd_ASMCreate($bDMO_MBAddRefASM64, $pPtr)
			$tSelf.pAddRef = $pPtr
			$tSelf.Release = __DSnd_ASMCreate($bDMO_MBReleaseASM64, $pPtr)
			$tSelf.pRelease = $pPtr
			$tSelf.SetLength = __DSnd_ASMCreate($bDMO_MBSetLengthASM64, $pPtr)
			$tSelf.pSetLength = $pPtr
			$tSelf.GetMaxLength = __DSnd_ASMCreate($bDMO_MBGetMaxLengthASM64, $pPtr)
			$tSelf.pGetMaxLength = $pPtr
			$tSelf.GetBufferAndLength = __DSnd_ASMCreate($bDMO_MBGetBufferAndLengthASM64, $pPtr)
			$tSelf.pGetBufferAndLength = $pPtr
	EndSwitch

	$tSelf.RefCnt = 1
	$tSelf.Size = DllStructGetSize($tBuffer)
	$tSelf.Buffer = DllStructGetPtr($tBuffer)

	Local $oSelf = ObjCreateInterface($pSelf, $sIID_IMediaBuffer, $tagIMediaBuffer)
	If Not IsObj($oSelf) Then Return SetError(1, 2, False)

	Return $oSelf
EndFunc   ;==>_DMO_MediaBufferCreate

; _DMO_MediaBufferGetBufferAndLength
; _DMO_MediaBufferGetMaxLength
; _DMO_MediaBufferSetLength

; ###############################################################################################################################
; # DMO Media ObjectInPlace
; ###############################################################################################################################
; _DMO_MediaObjectInPlaceClone
; _DMO_MediaObjectInPlaceGetLatency
; _DMO_MediaObjectInPlaceProcess

; ###############################################################################################################################
; # DMO MediaObject
; ###############################################################################################################################
; _DMO_MediaObjectAllocateStreamingResources
; _DMO_MediaObjectDiscontinuity
; _DMO_MediaObjectFlush
; _DMO_MediaObjectFreeStreamingResources
; _DMO_MediaObjectGetInputCurrentType
; _DMO_MediaObjectGetInputMaxLatency
; _DMO_MediaObjectGetInputSizeInfo
; _DMO_MediaObjectGetInputStatus
; _DMO_MediaObjectGetInputStreamInfo
; _DMO_MediaObjectGetInputType
; _DMO_MediaObjectGetOutputCurrentType
; _DMO_MediaObjectGetOutputSizeInfo
; _DMO_MediaObjectGetOutputStreamInfo
; _DMO_MediaObjectGetOutputType
; _DMO_MediaObjectGetStreamCount
; _DMO_MediaObjectLock
; _DMO_MediaObjectProcessInput
; _DMO_MediaObjectProcessOutput
; _DMO_MediaObjectSetInputMaxLatency

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaObjectSetInputType
; Description ...: Sets the media type on an input stream, or tests whether a media type is acceptable.
; Syntax ........: _DMO_MediaObjectSetInputType($oDMO[, $iChannels = 2[, $iSamplesPerSec = 44100[, $iBitsPerSample = 16[, $iFormatTag = $WAVE_FORMAT_PCM[, $sMediaSubType = $sMEDIASUBTYPE_PCM[, $iIdx = 0[, $iFlags = 0]]]]]]])
; Parameters ....: $oDMO                - This IMediaObject
;                  $iChannels           - Number of channels in the waveform-audio data.
;                  $iSamplesPerSec      - Sample rate, in samples per second (hertz).
;                  $iBitsPerSample      - Bits per sample for the $iFormatTag format type.
;                  $iFormatTag          - Waveform-audio format type.
;                  $sMediaSubType       - Subtype GUID of the stream.
;                  $iIdx                - Zero-based index of an input stream on the DMO.
;                  $iFlags              - Bitwise combination of zero or more flags from the DMO_SET_TYPE_FLAGS enumeration.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaObjectSetInputType($oDMO, $iChannels = 2, $iSamplesPerSec = 44100, $iBitsPerSample = 16, $iFormatTag = $WAVE_FORMAT_PCM, $sMediaSubType = $sMEDIASUBTYPE_PCM, $iIdx = 0, $iFlags = 0)
	If Not IsObj($oDMO) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $tWaveFormatEx = DllStructCreate($tagDSWAVEFORMATEX)
	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Local $iAvgBytesPerSec = $iSamplesPerSec * $iBlockAlign

	$tWaveFormatEx.FormatTag = $iFormatTag
	$tWaveFormatEx.Channels = $iChannels
	$tWaveFormatEx.SamplesPerSec = $iSamplesPerSec
	$tWaveFormatEx.AvgBytesPerSec = $iAvgBytesPerSec
	$tWaveFormatEx.BlockAlign = $iBlockAlign
	$tWaveFormatEx.BitsPerSample = $iBitsPerSample

	Local $tDMO_MEDIA_TYPE = DllStructCreate($tagDMO_MEDIA_TYPE)
	_WinAPI_GUIDFromStringEx($sMEDIATYPE_Audio, DllStructGetPtr($tDMO_MEDIA_TYPE, "MajorType"))
	_WinAPI_GUIDFromStringEx($sMediaSubType, DllStructGetPtr($tDMO_MEDIA_TYPE, "SubType"))
	_WinAPI_GUIDFromStringEx($sFORMAT_WaveFormatEx, DllStructGetPtr($tDMO_MEDIA_TYPE, "FormatType"))
	$tDMO_MEDIA_TYPE.Format = DllStructGetSize($tWaveFormatEx)
	$tDMO_MEDIA_TYPE.pFormat = DllStructGetPtr($tWaveFormatEx)

	Local $iHResult = $oDMO.SetInputType($iIdx, $tDMO_MEDIA_TYPE, $iFlags)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Return True
EndFunc   ;==>_DMO_MediaObjectSetInputType

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaObjectSetOutputType
; Description ...: Sets the media type on an output stream, or tests whether a media type is acceptable.
; Syntax ........: _DMO_MediaObjectSetOutputType($oDMO[, $iChannels = 2[, $iSamplesPerSec = 44100[, $iBitsPerSample = 16[, $iFormatTag = $WAVE_FORMAT_PCM[, $sMediaSubType = $sMEDIASUBTYPE_PCM[, $iIdx = 0[, $iFlags = 0]]]]]]])
; Parameters ....: $oDMO                - This IMediaObject
;                  $iChannels           - Number of channels in the waveform-audio data.
;                  $iSamplesPerSec      - Sample rate, in samples per second (hertz).
;                  $iBitsPerSample      - Bits per sample for the $iFormatTag format type.
;                  $iFormatTag          - Waveform-audio format type.
;                  $sMediaSubType       - Subtype GUID of the stream.
;                  $iIdx                - Zero-based index of an input stream on the DMO.
;                  $iFlags              - Bitwise combination of zero or more flags from the DMO_SET_TYPE_FLAGS enumeration.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaObjectSetOutputType($oDMO, $iChannels = 2, $iSamplesPerSec = 44100, $iBitsPerSample = 16, $iFormatTag = $WAVE_FORMAT_PCM, $sMediaSubType = $sMEDIASUBTYPE_PCM, $iIdx = 0, $iFlags = 0)
	If Not IsObj($oDMO) Then Return SetError($DSERR_NOOBJ, 0, False)

	Local $tWaveFormatEx = DllStructCreate($tagDSWAVEFORMATEX)
	Local $iBlockAlign = Floor($iChannels * ($iBitsPerSample / 8))
	Local $iAvgBytesPerSec = $iSamplesPerSec * $iBlockAlign

	$tWaveFormatEx.FormatTag = $iFormatTag
	$tWaveFormatEx.Channels = $iChannels
	$tWaveFormatEx.SamplesPerSec = $iSamplesPerSec
	$tWaveFormatEx.AvgBytesPerSec = $iAvgBytesPerSec
	$tWaveFormatEx.BlockAlign = $iBlockAlign
	$tWaveFormatEx.BitsPerSample = $iBitsPerSample

	Local $tDMO_MEDIA_TYPE = DllStructCreate($tagDMO_MEDIA_TYPE)
	_WinAPI_GUIDFromStringEx($sMEDIATYPE_Audio, DllStructGetPtr($tDMO_MEDIA_TYPE, "MajorType"))
	_WinAPI_GUIDFromStringEx($sMediaSubType, DllStructGetPtr($tDMO_MEDIA_TYPE, "SubType"))
	_WinAPI_GUIDFromStringEx($sFORMAT_WaveFormatEx, DllStructGetPtr($tDMO_MEDIA_TYPE, "FormatType"))
	$tDMO_MEDIA_TYPE.Format = DllStructGetSize($tWaveFormatEx)
	$tDMO_MEDIA_TYPE.pFormat = DllStructGetPtr($tWaveFormatEx)

	Local $iHResult = $oDMO.SetOutputType($iIdx, $tDMO_MEDIA_TYPE, $iFlags)
	If $iHResult Then Return SetError($iHResult, 1, False)

	Return True
EndFunc   ;==>_DMO_MediaObjectSetOutputType

; ###############################################################################################################################
; # DMO MediaParamInfo
; ###############################################################################################################################
; _DMO_MediaParamInfoGetCurrentTimeFormat
; _DMO_MediaParamInfoGetNumTimeFormats
; _DMO_MediaParamInfoGetParamCount
; _DMO_MediaParamInfoGetParamInfo
; _DMO_MediaParamInfoGetParamText
; _DMO_MediaParamInfoGetSupportedTimeFormat

; ###############################################################################################################################
; # DMO MediaParams
; ###############################################################################################################################

; #FUNCTION# ====================================================================================================================
; Name ..........: _DMO_MediaParamsAddEnvelope
; Description ...: Adds an envelope to a parameter.
; Syntax ........: _DMO_MediaParamsAddEnvelope($oMP, $aEnv[, $iIdx = 0])
; Parameters ....: $oMP                 - This IMediaParams object.
;                  $aEnv                - An Array:
;                                             - $aEnv[0][0] = Number of segments in the envelope.
;                                             - $aEnv[1][0] = Start time of the first segment, relative to the time stamp on the first buffer, in 100-nanosecond units.
;                                             - $aEnv[1][1] = Stop time of the segment, relative to the time stamp on the first buffer, in 100-nanosecond units.
;                                             - $aEnv[1][2] = Initial value of the parameter, at the start of the segment.
;                                             - $aEnv[1][3] = Final value of the parameter, at the end of the segment.
;                                             - $aEnv[1][4] = Member of the MP_CURVE_TYPE enumerated type that specifies the curve followed by the parameter.
;                                             - $aEnv[1][5] = Specifies one of the following flags:
;                                                                          - $MPF_ENVLP_STANDARD         = Use all the information provided with the envelope segment.
;                                                                          - $MPF_ENVLP_BEGIN_CURRENTVAL = Ignore the specified start value. Start from the current value.
;                                                                          - $MPF_ENVLP_BEGIN_NEUTRALVAL = Ignore the specified start value. Start from the neutral value.
;                                               ...
;                                             - $aEnv[n][0] = Start time of the n^th segment
;                                               ...
;                  $iIdx                - Zero-based index of the parameter, or $DWORD_ALLPARAMS to add the envelope to every parameter.
; Return values .: Success - True
;                  Failure - False
;                              | @error contains a non zero hresult value specifying the error code
;                              | @extended contains an index of the error position within the function
; Author ........: Eukalyptus
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......:
; ===============================================================================================================================
Func _DMO_MediaParamsAddEnvelope($oMP, $aEnv, $iIdx = 0)
	If Not IsObj($oMP) Then Return SetError($DSERR_NOOBJ, 0, False)
	If UBound($aEnv, 0) <> 2 Or UBound($aEnv, 2) < 6 Then Return SetError($DSERR_PARAM, 1, False)

	Local $iCnt = $aEnv[0][0]
	If $iCnt > UBound($aEnv) - 1 Then $iCnt = UBound($aEnv) - 1

	Local $tagENV = ""
	For $i = 1 To $iCnt
		$tagENV &= "struct; uint64; uint64; float; float; uint; uint; endstruct; "
	Next
	Local $tEnv = DllStructCreate($tagENV)
	For $i = 0 To $iCnt - 1
		For $j = 0 To 5
			DllStructSetData($tEnv, $i * 6 + $j + 1, $aEnv[$i + 1][$j])
		Next
	Next

	Local $iHResult = $oMP.AddEnvelope($iIdx, $iCnt, $tEnv)
	If $iHResult Then Return SetError($iHResult, 2, False)

	Return True
EndFunc   ;==>_DMO_MediaParamsAddEnvelope

; _DMO_MediaParamsFlushEnvelope
; _DMO_MediaParamsGetParam
; _DMO_MediaParamsSetParam
; _DMO_MediaParamsSetTimeFormat

; ###############################################################################################################################
; # DMO Internal
; ###############################################################################################################################

Func __DSnd_ASMCreate(Const ByRef $bBinaryCode, ByRef $pPtr)
	Local $iSize = BinaryLen($bBinaryCode)
	$pPtr = _MemVirtualAlloc(0, $iSize + 16, $MEM_COMMIT, $PAGE_EXECUTE_READWRITE)
	Local $pStruct = Number($pPtr)
	$pStruct = $pStruct + 16 - Mod($pStruct, 16)
	Local $tStruct = DllStructCreate("byte[" & $iSize & "];", $pStruct)
	DllStructSetData($tStruct, 1, $bBinaryCode)
	Return $pStruct
EndFunc   ;==>__DSnd_ASMCreate