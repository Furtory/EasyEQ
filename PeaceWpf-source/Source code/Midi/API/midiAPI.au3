#include-once
#include <midiApiConstants.au3>

; #INDEX# =======================================================================================================================
; Title .........: Midi API
; AutoIt Version : 3.3.16.0
; Description ...: Midi API wrapper
; Author(s) .....: MattyD
; Dll ...........: winmm.dll
; ===============================================================================================================================

; #VARIABLES# ===================================================================================================================
Global $__g_hWinMMDll
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;_midiAPI_Connect
;_midiAPI_Disconnect
;_midiAPI_InAddBuffer
;_midiAPI_InClose
;_midiAPI_InGetDevCaps
;_midiAPI_InGetErrorText
;_midiAPI_InGetNumDevs
;_midiAPI_InOpen
;_midiAPI_InPrepareHeader
;_midiAPI_InReset
;_midiAPI_InStart
;_midiAPI_InStop
;_midiAPI_InUnprepareHeader
;_midiAPI_OutCacheDrumPatches
;_midiAPI_OutCachePatches
;_midiAPI_OutClose
;_midiAPI_OutGetDevCaps
;_midiAPI_OutGetErrorText
;_midiAPI_OutGetNumDevs
;_midiAPI_OutGetVolume
;_midiAPI_OutLongMsg
;_midiAPI_OutOpen
;_midiAPI_OutPrepareHeader
;_midiAPI_OutReset
;_midiAPI_OutSetVolume
;_midiAPI_OutShortMsg
;_midiAPI_OutUnprepareHeader
;_midiAPI_Shutdown
;_midiAPI_Startup
;_midiAPI_StreamClose
;_midiAPI_StreamOpen
;_midiAPI_StreamOut
;_midiAPI_StreamPause
;_midiAPI_StreamPosition
;_midiAPI_StreamProperty
;_midiAPI_StreamRestart
;_midiAPI_StreamStop
; ===============================================================================================================================

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_Connect
; Description ...: Connects a MIDI input device to a MIDI thru or output device, or connects a MIDI thru device to a MIDI output
;                  device.
; Syntax.........: _midiAPI_Connect($hInDevice, $hOutDevice)
; Parameters ....: $hInDevice  - MidiIn or MidiThru device handle
;                  $hOutDevice - MidiThru or MidiOut device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_NOTREADY     - Input device is already connected.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: Use the _midiAPI_OutOpen function to obtain a handle to a thru device.
;                  A thru device can be connected to multiple out devices.
;                  The input device must be opened with a callback mechanism for the throughput of midi data.
; Related .......: _midiAPI_Disconnect
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_Connect($hInDevice, $hOutDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiConnect", _
			"hwnd", $hInDevice, "hwnd", $hOutDevice, "int", 0)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_Connect

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_Disconnect
; Description ...: Disconnects a MIDI input device from a MIDI thru or output device, or disconnects a MIDI thru device from a
;                  MIDI output device
; Syntax.........: _midiAPI_Disconnect($hInDevice, $hOutDevice)
; Parameters ....: $hInDevice  - MidiIn or MidiThru device handle
;                  $hOutDevice - MidiThru or MidiOut device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......:
; Related .......: _midiAPI_Connect
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_Disconnect($hInDevice, $hOutDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiDisconnect", _
			"hwnd", $hInDevice, "hwnd", $hOutDevice, "int", 0)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_Disconnect

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InAddBuffer
; Description ...: The midiInAddBuffer function sends an input buffer to a specified opened MIDI input device.
;                  This function is used for system-exclusive messages.
; Syntax.........: _midiAPI_InAddBuffer($hDevice, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hDevice    - Midi device handle
;                  $pMidiHdr   - Pointer to a midi header structure
;                  $iMidiHdrSz - Size of the midi header structure (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_STILLPLAYING - Buffer is still in the queue.
;                  |$MIDIERR_UNPREPARED   - Buffer has not been prepared.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: The buffer must be prepared by using the midiInPrepareHeader function before it is passed to midiInAddBuffer.
;                  An input buffer is not required for MIM_DATA (short) messages.
;                  Buffers are returned to the client when they are full, when a complete system-exclusive message has been
;                  received, or when the _midiAPI_InReset function is used. The dwBytesRecorded member of the MIDIHDR structure will
;                  contain the actual length of data received
; Related .......: _midiAPI_InPrepareHeader, _midiAPI_InReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InAddBuffer($hDevice, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInAddBuffer", _
			"hwnd", $hDevice, "ptr", $pMidiHdr, "uint", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InAddBuffer

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InClose
; Description ...: Closes a MIDI input device.
; Syntax.........: _midiAPI_InClose($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_STILLPLAYING - Buffer is still in the queue.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: If a buffer sent by _midiAPI_InAddBuffer has not been returned to the application this function will fail. Use
;                  _midiAPI_InReset to return all pending buffers.
; Related .......: _midiAPI_InOpen, _midiAPI_InReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InClose($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInClose", "hwnd", $hDevice)
	If @error Then Return SetError(-1, @error, False)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, False)
	Return True
EndFunc   ;==>_midiAPI_InClose

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InGetDevCaps
; Description ...: Retrieves capabilities of a midiIn device.
; Syntax.........: _midiAPI_InGetDevCaps($iDeviceID, $pMidiInCaps [, $iMidiInCapsSz = 76])
; Parameters ....: $iDeviceID     - Identifier of the MIDI input device.
;                  $pMidiInCaps   - Pointer to a MIDIINCAPS structure
;                  $iMidiInCapsSz - Size of the struct (76 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_BADDEVICEID - The specified device identifier is out of range.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NODRIVER    - Then driver is not installed.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $iDeviceID can be - A midiIn device ID: 0 to (_midiAPI_InGetNumDevs() - 1)
;                                    - A midiIn device handle.
;                  DllStructCreate($tag_midiAPI_incaps) can be used to create the struct.
;                  $tag_midiAPI_incaps members:
;                              wMid            - Manufacturer ID
;                              wPid            - Product ID
;                              vDriverVersion  - High-order byte: major version [BitShift(vDriverVersion, 8)]
;                                              - Low-order byte: minor version [BitAND(vDriverVersion, 0x00FF)]
;                              szPname         - Product name
; Related .......: _midiAPI_InGetNumDevs
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InGetDevCaps($iDeviceID, $pMidiInCaps, $iMidiInCapsSz = 76)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInGetDevCapsW", _
			"uint_ptr", $iDeviceID, "ptr", $pMidiInCaps, "uint", $iMidiInCapsSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InGetDevCaps

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InGetErrorText
; Description ...: Retrieves a textual description for an error identified by the specified error code.
; Syntax.........: _midiAPI_InGetErrorText($iError)
; Parameters ....: $iError     - A multimedia error code
; Return values .: Success: An error description, @error = 0
;                  Failure: A blank string "", @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_BADERRNUM   - Error number is out of range.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: Can be used interchangeably with _midiAPI_OutGetErrorText
; Related .......: _midiAPI_OutGetErrorText
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InGetErrorText($iError)
	Local $tBuff = DllStructCreate(StringFormat("wchar[%d]", $MAXERRORLENGTH))
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInGetErrorTextW", _
			"int", $iError, "ptr", DllStructGetPtr($tBuff), "uint", $MAXERRORLENGTH)
	If @error Then Return SetError(-1, @error, "")
	Return SetError($aRes[0], 0, DllStructGetData($tBuff, 1))
EndFunc   ;==>_midiAPI_InGetErrorText

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InGetNumDevs
; Description ...: Retrieves the number of MIDI input devices in the system.
; Syntax.........: _midiAPI_InGetNumDevs()
; Parameters ....: None.
; Return values .: Success: The number of midiIn devices found, @error = 0
;                  Failure: 0, @error <> 0
;                  @error: -1  - Dll call failure. @extended set to DllCall() error
; Author ........: MattyD
; Modified.......:
; Remarks .......:
; Related .......: _midiAPI_InGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InGetNumDevs()
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInGetNumDevs")
	If @error Then Return SetError(-1, @error, 0)
	Return $aRes[0]
EndFunc   ;==>_midiAPI_InGetNumDevs

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InOpen
; Description ...: Opens a midiIn device.
; Syntax.........: _midiAPI_InOpen($iDeviceID [, $pCallBk = 0 [, $iInst = 0 [, $iFlags = $CALLBACK_NULL]]] )
; Parameters ....: $iDeviceID - Identifier of the MIDI input device.
;                  $pCallBk   - Pointer to a callback function, or a handle of a window
;                  $iInst     - User "instance id" passed to the callback function.
;                               This parameter is not used with window callback functions
;                  $iFlags    - Callback flag for opening the device and, optionally, a status flag that helps regulate rapid
;                               data transfers.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_ALLOCATED   - The specified resource is already allocated.
;                  |$MMSYSERR_BADDEVICEID - The specified device identifier is out of range.
;                  |$MMSYSERR_INVALFLAG   -	The flags specified by dwFlags are invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $iDeviceID is an integer from: 0 to (_midiAPI_InGetNumDevs() - 1)
;                  $iFlags values can be:
;                       $CALLBACK_NULL - $pCallBk must be 0
;                       $CALLBACK_FUNCTION - $pCallBk is pointer to a callback function. This method can be unstable in AutoIt.
;                                          | The callback function can be defined as:
;                                          | DllCallbackRegister("MidiInProc", "none", "hwnd;uint;dword_ptr;dword_ptr;dword_ptr")
;                                          | The callback function has five parameters
;                                              - $hMidiIn - Handle to the MIDI input device
;                                              - $iMsg    - The $MIM_* midi message
;                                              - $iInst   - The user data specified when calling _midiAPI_InOpen
;                                              - $iParam1 - Message parameter.
;                                              - $iParam2 - Message parameter.(Usually time elapsed in ms from _midiAPI_InStart)
;                       $CALLBACK_WINDOW   - $pCallBk is a window handle
;                                          | $MM_MIM_* messages can be registered GUIRegisterMsg
;                                          | The callback function has four parameters
;                                              - $hWnd   - The window handle
;                                              - $iMsg    - The $MM_MIM_* midi message
;                                              - $wParam  - The midi device handle
;                                              - $lParam  - Message parameter.
;                       $MIDI_IO_STATUS    - Can be combined with $CALLBACK_FUNCTION or $CALLBACK_WINDOW
;                                          | Specifies [MM_]MIM_MOREDATA messages should also be sent to the callback.
; Related .......: _midiAPI_InGetNumDevs, _midiAPI_InGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InOpen($iDeviceID, $pCallBk = 0, $iInst = 0, $iFlags = $CALLBACK_NULL)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInOpen", _
			"hwnd*", 0, "int", $iDeviceID, "dword_ptr", $pCallBk, "int", $iInst, "int", $iFlags)
	If @error Then Return SetError(-1, @error, 0)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, 0)
	Return $aRes[1]
EndFunc   ;==>_midiAPI_InOpen

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InPrepareHeader
; Description ...: Prepares a buffer for MIDI input
; Syntax.........: _midiAPI_InPrepareHeader($hDevice, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hDevice    - Midi device handle
;                  $pMidiHdr   - Pointer to a MIDIHDR structure
;                  $iMidiHdrSz - Size of the struct (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: _midiAPI_InPrepareHeader must be called before _midiAPI_InAddBuffer
;                  Multiple buffers can be allocated to a midi device
;                  Before freeing the buffer _midiAPI_InUnprepareHeader must be called
;                  DllStructCreate($tag_midiAPI_hdr) can be used to create the MIDIHDR struct.
;                  These members must be set before calling _midiAPI_InPrepareHeader:
;                              lpData        - Pointer to buffer
;		                       wBufferLength - Size of the buffer
;		                       dwFlags       - Must be 0
; Related .......: _midiAPI_InAddBuffer, _midiAPI_InUnprepareHeader
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InPrepareHeader($hDevice, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInPrepareHeader", _
			"hwnd", $hDevice, "ptr", $pMidiHdr, "uint", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InPrepareHeader

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InReset
; Description ...: Stops input on a given MIDI input device.
; Syntax.........: _midiAPI_InReset($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: This function is for use with system-exclusive messages. This is not needed when handling general midi input.
;                  If a buffer has not been returned to the application the _midiAPI_InClose function will fail. Use _midiAPI_InReset
;                  to return all pending buffers.
; Related .......: _midiAPI_InAddBuffer, _midiAPI_InClose
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InReset($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInReset", "hwnd", $hDevice)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InReset

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InStart
; Description ...: Starts MIDI input on the specified MIDI input device.
; Syntax.........: _midiAPI_InStart($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: Timstamps in callback messages is relative to the time this function was called
;                  All bar system-exclusive messages are sent directly to the callback mechanism. SysExc messages are placed in
;                  buffers allocated by _midiAPI_InAddBuffer.
; Related .......: _midiAPI_InStop, _midiAPI_InReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InStart($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInStart", "hwnd", $hDevice)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InStart

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InStop
; Description ...: Stops input on a given MIDI input device.
; Syntax.........: _midiAPI_InStop($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: If there are any system-exclusive messages or stream buffers in the queue, the current buffer is marked as
;                  done, but any empty buffers in the queue remain there and are not marked as done.
; Related .......: _midiAPI_InStop, _midiAPI_InReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InStop($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInStop", "hwnd", $hDevice)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InStop

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_InUnprepareHeader
; Description ...: Cleans up the preparation performed by the _midiAPI_InPrepareHeader function.
; Syntax.........: _midiAPI_InUnprepareHeader($hDevice, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hDevice    - Midi device handle
;                  $pMidiHdr   - Pointer to a MIDIHDR structure
;                  $iMidiHdrSz - Size of the struct (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_STILLPLAYING - Buffer is still in the queue.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: This function complements _midiAPI_InPrepareHeader. _midiAPI_InUnprepareHeader must be called before freeing the
;                  buffer that is pointed to in the MIDIHDR stucture.
;                  The driver must be finished with the buffer before calling, otherwise the function will fail. You can check
;                  the status of the buffer by inspecting the dwFlags member of the MIDIHDR struct.
; Related .......: _midiAPI_InPrepareHeader, _midiAPI_InReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_InUnprepareHeader($hDevice, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiInUnprepareHeader", _
			"hwnd", $hDevice, "ptr", $pMidiHdr, "uint", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_InUnprepareHeader

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutCacheDrumPatches
; Description ...: Requests that an internal synthesizer preloads and caches a set of key-based percussion patches.
; Syntax.........: _midiAPI_OutCacheDrumPatches($hDevice, $iPatch, $pKeyArr, $iFlags)
; Parameters ....: $hDevice    - Midi device handle
;                  $iPatch     - Patch number that should be used. 0 indicates the default drum patch.
;                  $pKeyArr    - Pointer to a KEYARRAY struct.
;                  $iFlags     - Options for the cache operation.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1              - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALFLAG    - The flags specified by dwFlags are invalid.
;                  |$MMSYSERR_INVALHANDLE  - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM   - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM        - The system is unable to allocate or lock memory.
;                  |$MMSYSERR_NOTSUPPORTED - The device does not support patch caching.
; Author ........: MattyD
; Modified.......:
; Remarks .......: This function applies only to internal MIDI synthesizers. Some synths are not capable of keeping all
;                  percussion patches loaded simultaneously. Caching patches ensures these are available.
;                  If a device supports patch caching, the $MIDICAPS_CACHE flag will be present when calling _midiAPI_OutGetDevCaps
;                  DllStructCreate($tag_keyarray) can be used to create the KEYARRAY struct.
;                      - Each element in the key array represents one of the 128 key-based percussion patches.
;                      - Each bit of the element represents one the 16 MIDI channels that use the particular patch, with the
;                        least-significant bit representing channel 0.
;                  For example, if the patch on key number 60 is used by physical channels 9 and 15, element 60 would be 0x8200.
;                  $iFlags values can be:
;                          $MIDI_CACHE_ALL     - Caches all of the specified patches. If they cannot all be cached, it caches
;                                                none, clears the KEYARRAY array, and sets @error to $MMSYSERR_NOMEM.
;                          $MIDI_CACHE_BESTFIT - Caches all of the specified patches. If they cannot all be cached, it caches
;                                                as many patches as possible, changes the KEYARRAY array to reflect which patches
;                                                were cached, and sets @error to $MMSYSERR_NOMEM.
;                          $MIDI_CACHE_QUERY   - Changes the KEYARRAY array to indicate which patches are currently cached.
;                          $MIDI_UNCACHE       - Uncaches the specified patches and clears the KEYARRAY array.
; Related .......: _midiAPI_OutCachePatches, _midiAPI_OutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutCacheDrumPatches($hDevice, $iPatch, $pKeyArr, $iFlags)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutCacheDrumPatches", _
			"hwnd", $hDevice, "uint", $iPatch, "ptr", $pKeyArr, "uint", $iFlags)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutCacheDrumPatches

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutCachePatches
; Description ...: Requests that an internal synthesizer preloads and caches a set of patches.
; Syntax.........: _midiAPI_OutCachePatches($hDevice, $iPatch, $pPatchArr, $iFlags)
; Parameters ....: $hDevice    - Midi device handle
;                  $iBank      - Bank of patches that should be used. 0 indicates the default patch bank.
;                  $pPatchArr  - Pointer to a PATCHARRAY struct.
;                  $iFlags     - Options for the cache operation.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1              - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALFLAG    - The flags specified by dwFlags are invalid.
;                  |$MMSYSERR_INVALHANDLE  - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM   - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM        - The system is unable to allocate or lock memory.
;                  |$MMSYSERR_NOTSUPPORTED - The device does not support patch caching.
; Author ........: MattyD
; Modified.......:
; Remarks .......: This function applies only to internal MIDI synthesizers. Some synths are not capable of keeping all
;                  patches loaded simultaneously. Caching patches ensures these are available.
;                  If a device supports patch caching, the $MIDICAPS_CACHE flag will be present when calling _midiAPI_OutGetDevCaps
;                  DllStructCreate($tag_patcharray) can be used to create the PATCHARRAY struct.
;                      - Each element in the patch array represents one of the 128 patches.
;                      - Each bit of the element represents one the 16 MIDI channels that use the particular patch, with the
;                        least-significant bit representing channel 0.
;                  For example, if patch 0 is used by physical channels 0 and 8, element 0 would be set to 0x0101.
;                  $iFlags values can be:
;                          $MIDI_CACHE_ALL     - Caches all of the specified patches. If they cannot all be cached, it caches
;                                                none, clears the PATCHARRAY array, and sets @error to $MMSYSERR_NOMEM.
;                          $MIDI_CACHE_BESTFIT - Caches all of the specified patches. If they cannot all be cached, it caches
;                                                as many patches as possible, changes the PATCHARRAY array to reflect which
;                                                patches were cached, and sets @error to $MMSYSERR_NOMEM.
;                          $MIDI_CACHE_QUERY   - Changes the PATCHARRAY array to indicate which patches are currently cached.
;                          $MIDI_UNCACHE       - Uncaches the specified patches and clears the PATCHARRAY array.
; Related .......: _midiAPI_OutCacheDrumPatches, _midiAPI_OutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutCachePatches($hDevice, $iBank, $pPatchArr, $iFlags)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutCachePatches", _
			"hwnd", $hDevice, "uint", $iBank, "ptr", $pPatchArr, "uint", $iFlags)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutCachePatches

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutClose
; Description ...: Closes a MIDI output device.
; Syntax.........: _midiAPI_OutClose($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_STILLPLAYING - Buffer is still in the queue.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: If a buffer sent by _midiAPI_OutLongMessage has not been returned to the application this function will fail.
;                  Use _midiAPI_OutReset to mark all pending buffers as done.
; Related .......: _midiAPI_OutOpen, _midiAPI_OutReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutClose($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutClose", "hwnd", $hDevice)
	If @error Then Return SetError(-1, @error, False)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, False)
	Return True
EndFunc   ;==>_midiAPI_OutClose

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutGetDevCaps
; Description ...: Retrieves capabilities of a midiOut device.
; Syntax.........: _midiAPI_OutGetDevCaps($iDeviceID, $pMidiOutCaps [, $iMidiOutCapsSz = 84])
; Parameters ....: $iDeviceID      - Identifier of the midi output device.
;                  $pMidiOutCaps   - Pointer to a MIDIOUTCAPS structure
;                  $iMidiOutCapsSz - Size of the struct (84 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_BADDEVICEID - The specified device identifier is out of range.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NODRIVER    - Then driver is not installed.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $iDeviceID can be - A midiOut device ID: 0 to (_midiAPI_OutGetNumDevs() - 1)
;                                    - A midiOut device handle.
;                                    - $MIDI_MAPPER constant. (will retrieve the "default" device)
;                  DllStructCreate($tag_midiAPI_outcaps) can be used to create the struct.
;                  $tag_midiAPI_outcaps members:
;                              wMid            - Manufacturer ID
;                              wPid            - Product ID
;                              vDriverVersion  - High-order byte: major version [BitShift(vDriverVersion, 8)]
;                                              - Low-order byte: minor version [BitAND(vDriverVersion, 0x00FF)]
;                              szPname         - Product name
;                              wTechnology     - One of the following:
;                                              |$MOD_midiAPI_PORT      - MIDI hardware port.
;                                              |$MOD_SYNTH         - Synthesizer.
;                                              |$MOD_SQSYNTH       - Square wave synthesizer.
;                                              |$MOD_FMSYNTH       - FM synthesizer.
;                                              |$MOD_MAPPER        - Microsoft MIDI mapper.
;                                              |$MOD_WAVETABLE     - Hardware wavetable synthesizer.
;                                              |$MOD_SWSYNTH       - Software synthesizer.
;                              wVoices         - Number of voices (internal synth only)
;                              wVoices         - Max number of notes (internal synth only)
;                              wChannelMask    - Channels used (internal synth only)
;                              dwSupport       - One or more of the following: (optionally supported by the device)
;                                              |$MIDICAPS_CACHE    - Supports patch caching.
;                                              |$MIDICAPS_LRVOLUME - Supports separate left and right volume control.
;                                              |$MIDICAPS_STREAM   - Provides direct support for the midiStreamOut function.
;                                              |$MIDICAPS_VOLUME   - Supports volume control.
; Related .......: _midiAPI_InGetNumDevs
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutGetDevCaps($iDeviceID, $pMidiOutCaps, $iMidiOutCapsSz = 84)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutGetDevCapsW", _
			"uint_ptr", $iDeviceID, "ptr", $pMidiOutCaps, "uint", $iMidiOutCapsSz)
	If @error Then Return SetError(-1, @error, 0)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutGetDevCaps

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutGetErrorText
; Description ...: Retrieves a textual description for an error identified by the specified error code.
; Syntax.........: _midiAPI_OutGetErrorText($iError)
; Parameters ....: $iError     - A multimedia error code
; Return values .: Success: An error description, @error = 0
;                  Failure: A blank string "", @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_BADERRNUM   - Error number is out of range.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: Can be used interchangeably with _midiAPI_InGetErrorText
; Related .......: _midiAPI_InGetErrorText
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutGetErrorText($iError)
	Local $tBuff = DllStructCreate(StringFormat("wchar[%d]", $MAXERRORLENGTH))
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutGetErrorTextW", _
			"int", $iError, "ptr", DllStructGetPtr($tBuff), "uint", $MAXERRORLENGTH)
	If @error Then Return SetError(-1, @error, "")
	Return SetError($aRes[0], 0, DllStructGetData($tBuff, 1))
EndFunc   ;==>_midiAPI_OutGetErrorText

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutGetNumDevs
; Description ...: Retrieves the number of MIDI input devices in the system.
; Syntax.........: _midiAPI_OutGetNumDevs()
; Parameters ....: None.
; Return values .: Success: The number of midiOut devices found, @error = 0
;                  Failure: 0, @error <> 0
;                  @error: -1  - Dll call failure. @extended set to DllCall() error
; Author ........: MattyD
; Modified.......:
; Remarks .......:
; Related .......: _midiAPI_OutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutGetNumDevs()
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutGetNumDevs")
	If @error Then Return SetError(-1, @error, 0)
	Return $aRes[0]
EndFunc   ;==>_midiAPI_OutGetNumDevs

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutGetVolume
; Description ...: Retrieves the current volume setting of a MIDI output or stream device.
; Syntax.........: _midiAPI_OutGetVolume($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: The current volume setting, @error = 0
;                  Failure: -1, @error <> 0
;                  @error: -1  - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE  - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM   - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM        - The system is unable to allocate or lock memory.
;                  |$MMSYSERR_NOTSUPPORTED - The function is not supported.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $hDevice can be:
;                        | A midiOut device handle
;                        | A midiStream handle
;                        | A midiOut device ID: 0 to (_midiAPI_OutGetNumDevs() - 1)
;                        | $MIDI_MAPPER constant.
;                  The returned value is dependent on the devices capabilities.
;                  If $MIDICAPS_LRVOLUME if present when calling _midiAPI_OutGetDevCaps, the function returns:
;                                               - High-order word: Right Channel [BitShift(vDriverVersion, 16)]
;                                               - Low-order word: Left Channel [BitAND(vDriverVersion, 0xFFFF)]
;                  If $MIDICAPS_VOLUME is present but $MIDICAPS_LRVOLUME is not present, the function returns:
;                                                - Low-order word: Mono Level
;                  Levels range from 0 to 0xFFFF.
;                  Any value set by using the _midiAPI_OutSetVolume function is returned, regardless of whether the device
;                  supports that value.
;                  If a device identifier is used, then the result of the _midiAPI_OutGetVolume call applies to all instances of
;                  the device. If a device handle is used, then the information returned applies only to the instance of the
;                  device referenced by the device handle.
; Related .......: _midiAPI_OutSetVolume, _midiAPI_OutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutGetVolume($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutGetVolume", _
			"hwnd", $hDevice, "dword*", 0)
	If @error Then Return SetError(-1, @error, 0)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, -1)
	Return $aRes[2]
EndFunc   ;==>_midiAPI_OutGetVolume

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutLongMsg
; Description ...: Sends a system-exclusive MIDI message to the specified MIDI output device.
; Syntax.........: _midiAPI_OutLongMsg($hDevice, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hDevice    - Midi device handle
;                  $pMidiHdr   - Pointer to a midi header structure
;                  $iMidiHdrSz - Size of the midi header structure (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_NOTREADY     - The hardware is busy with other data.
;                  |$MIDIERR_UNPREPARED   - Buffer has not been prepared.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: The buffer must be prepared using the midiOutPrepareHeader function before it is passed to
;                  _midiAPI_OutLongMsg. For general midi messages use _midiAPI_OutShortMsg
; Related .......: _midiAPI_OutPrepareHeader, _midiAPI_OutShortMsg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutLongMsg($hDevice, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutLongMsg", _
			"hwnd", $hDevice, "ptr", $pMidiHdr, "int", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutLongMsg

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutOpen
; Description ...: Opens a midiOut device.
; Syntax.........: _midiAPI_OutOpen($iDeviceID [, $pCallBk = 0 [, $iInst = 0 [, $iFlags = $CALLBACK_NULL]]] )
; Parameters ....: $iDeviceID - Identifier of the MIDI out device.
;                  $pCallBk   - Pointer to a callback function, or a handle of a window
;                  $iInst     - User "instance id" passed to the callback function.
;                               This parameter is not used with window callback functions
;                  $iFlags    - Callback flag for opening the device and.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_NODEVICE     - No MIDI port was found. This error occurs only when the mapper is opened.
;                  |$MMSYSERR_ALLOCATED   - The specified resource is already allocated.
;                  |$MMSYSERR_BADDEVICEID - The specified device identifier is out of range.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $iDeviceID is an integer from: 0 to (_midiAPI_InGetNumDevs() - 1) or the $MIDI_MAPPER constant.
;                  $iFlags values can be:
;                       $CALLBACK_NULL     - $pCallBk must be 0
;                       $CALLBACK_EVENT    - $pCallBk is an event handle. (Out of this UDF Scope). Lookup CreateEventW() for
;                                            more info.
;                       $CALLBACK_FUNCTION - $pCallBk is pointer to a callback function. This method can be unstable in AutoIt.
;                                          | The callback function can be defined as:
;                                          | DllCallbackRegister("MidiOutProc", "none", "hwnd;uint;dword_ptr;dword_ptr;dword_ptr")
;                                          | The callback function has five parameters
;                                              - $hMidiIn - Handle to the MIDI input device
;                                              - $iMsg    - The $MOM_* midi message
;                                              - $iInst   - The user data specified when calling _midiAPI_InOpen
;                                              - $iParam1 - Message parameter.
;                                              - $iParam2 - Message parameter.(Usually reserved)
;                       $CALLBACK_WINDOW   - $pCallBk is a window handle
;                                          | $MM_MIM_* messages can be registered GUIRegisterMsg()
;                                          | The callback function has four parameters
;                                              - $hWnd   - The window handle
;                                              - $iMsg    - The $MM_MOM_* midi message
;                                              - $wParam  - The midi device handle
;                                              - $lParam  - Message parameter.
; Related .......: _midiAPI_OutGetNumDevs, _midOutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutOpen($iDeviceID, $pCallBk = 0, $iInst = 0, $iFlags = $CALLBACK_NULL)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutOpen", _
			"hwnd*", 0, "int", $iDeviceID, "dword_ptr", $pCallBk, "int", $iInst, "int", $iFlags)
	If @error Then Return SetError(-1, @error, 0)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, 0)
	Return $aRes[1]
EndFunc   ;==>_midiAPI_OutOpen

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutPrepareHeader
; Description ...: Prepares a MIDI system-exclusive or stream buffer for output.
; Syntax.........: _midiAPI_OutPrepareHeader($hDevice, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hDevice    - Midi device handle
;                  $pMidiHdr   - Pointer to a MIDIHDR structure
;                  $iMidiHdrSz - Size of the struct (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: _midiAPI_OutPrepareHeader must be called before _midiAPI_OutLongMessage or _midiAPI_StreamOut.
;                  Multiple buffers can be allocated to a midi device
;                  Before freeing the buffer _midiAPI_OutUnprepareHeader must be called.
;                  DllStructCreate($tag_midiAPI_hdr) can be used to create the MIDIHDR struct.
;                  These members must be set before calling _midiAPI_OutPrepareHeader:
;                              lpData        - Pointer to buffer
;		                       wBufferLength - Size of the buffer
;		                       dwFlags       - Must be 0
;                  A stream buffer cannot be larger than 64K.
; Related .......: _midiAPI_OutLongMessage, _midiAPI_StreamOut, _midiAPI_OutUnprepareHeader
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutPrepareHeader($hDevice, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutPrepareHeader", _
			"hwnd", $hDevice, "ptr", $pMidiHdr, "uint", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutPrepareHeader

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutReset
; Description ...: Turns off all notes on all MIDI channels for the specified MIDI output device.
; Syntax.........: _midiAPI_OutReset($hDevice)
; Parameters ....: $hDevice    - Midi device handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $hDevice can also be a midiStream handle.
;                  When called, any pending system-exclusive or stream output buffers are returned to the callback function
;                  and the $MHDR_DONE flag is set in the MIDIHDR structure.
;                  Terminating a system-exclusive message without sending an EOX (end-of-exclusive) byte might cause problems
;                  for the receiving device. (The midiOutReset function does not send an EOX byte when it terminates a
;                  system-exclusive message)
;                  To turn off all notes, a note-off message for each note in each channel is sent. In addition, the sustain
;                  controller is turned off for each channel.
; Related .......: _midiAPI_OutLongMsg, _midiAPI_StreamOut
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutReset($hDevice)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutReset", "hwnd", $hDevice)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutReset

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutSetVolume
; Description ...: Sets the volume of a MIDI output device.
; Syntax.........: _midiAPI_OutSetVolume($hDevice, $iVolume)
; Parameters ....: $hDevice    - Midi device handle
;                  $iVolume    - Volume parameter:
;                              | High-order word: Right Channel Level
;                              | Low-order word:  Left Channel/Mono Level
; Return values .: Success: True, @error = 0
;                  Failure: -1, @error <> 0
;                  @error: -1  - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE  - Device handle is invalid.
;                  |$MMSYSERR_NOMEM        - The system is unable to allocate or lock memory.
;                  |$MMSYSERR_NOTSUPPORTED - The function is not supported.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $hDevice can be:
;                        | A midiOut device handle
;                        | A midiStream handle
;                        | A midiOut device ID: 0 to (_midiAPI_OutGetNumDevs() - 1)
;                        | $MIDI_MAPPER constant.
;                  If a device identifier is used, then the result of the _midiAPI_OutSetVolume call applies to all instances of
;                  the device. If a device handle is used, then the information returned applies only to the instance of the
;                  device referenced by the device handle.
;                  Call _midiAPI_OutGetDevCaps to determine if the device supports this function, and/or independent left and
;                  right channel levels. $MIDICAPS_LRVOLUME and $MIDICAPS_VOLUME are returned as indicators.
;                  Levels can range from 0 to 0xFFFF. Devices that do not support a full 16 bits of volume-level control
;                  use the high-order bits of the requested volume setting.
;                  The midiOutGetVolume function returns the full 16-bit value, as set by midiOutSetVolume, irrespective of the
;                  device's capabilities.
; Related .......: _midiAPI_OutGetVolume, _midiAPI_OutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutSetVolume($hDevice, $iVolume)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutSetVolume", _
			"hwnd", $hDevice, "dword", $iVolume)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutSetVolume

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutShortMsg
; Description ...: Sends a short (max 3 byte) MIDI message to the specified MIDI output device.
; Syntax.........: _midiAPI_OutShortMsg($hDevice, $iMessage)
; Parameters ....: $hDevice    - Midi device handle
;                  $iMessage   - A MIDI message, with the low-order byte representing the first (status) byte.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_BADOPENMODE  - The application sent a message without a status byte.
;                  |$MIDIERR_NOTREADY     -	The hardware is busy with other data.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $hDevice can also be a midiStream handle.
;                  When a series of messages have the same status byte, the status byte can be omitted from messages after the
;                  first one in the series, creating a running status.
; Related .......: _midiAPI_OutLongMsg, _midiAPI_StreamOut
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutShortMsg($hDevice, $iMessage)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutShortMsg", _
			"hwnd", $hDevice, "dword", $iMessage)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutShortMsg

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_OutUnprepareHeader
; Description ...: Cleans up the preparation performed by the _midiAPI_OutPrepareHeader function.
; Syntax.........: _midiAPI_OutUnprepareHeader($hDevice, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hDevice    - Midi device handle
;                  $pMidiHdr   - Pointer to a MIDIHDR structure
;                  $iMidiHdrSz - Size of the struct (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_STILLPLAYING - Buffer is still in the queue.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: $hDevice can also be a midiStream handle.
;                  This function complements _midiAPI_OutPrepareHeader. _midiAPI_OutUnprepareHeader must be called before freeing the
;                  buffer that is pointed to in the MIDIHDR stucture.
;                  The driver must be finished with the buffer before calling, otherwise the function will fail. You can check
;                  the status of the buffer by inspecting the dwFlags member of the MIDIHDR struct.
; Related .......: _midiAPI_OutPrepareHeader., _midiAPI_OutReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_OutUnprepareHeader($hDevice, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiOutUnprepareHeader", _
			"hwnd", $hDevice, "ptr", $pMidiHdr, "uint", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_OutUnprepareHeader

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_Shutdown
; Description ...: Closes the dll file
; Syntax.........: _midiAPI_Shutdown()
; Parameters ....: None.
; Return values .: None.
; Author ........: MattyD
; Modified.......:
; Remarks .......:
; Related .......: _midiAPI_Startup, DllClose
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func _midiAPI_Shutdown()
	DllClose($__g_hWinMMDll)
EndFunc   ;==>_midiAPI_Shutdown

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_Startup
; Description ...: Opens the dll file for use with subsequent functions.
; Syntax.........: _midiAPI_Startup()
; Parameters ....: None.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error = error from DllOpen() function.
; Author ........: MattyD
; Modified.......:
; Remarks .......:
; Related .......: _midiAPI_Startup, DllOpen
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func _midiAPI_Startup()
	$__g_hWinMMDll = DllOpen("winmm.dll")
	Return SetError(@error, @extended, $__g_hWinMMDll >= 0)
EndFunc   ;==>_midiAPI_Startup

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamClose
; Description ...: closes an open MIDI stream.
; Syntax.........: _midiAPI_StreamClose($hStream)
; Parameters ....: $hStream    - Midi stream handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......:
; Related .......: _midiAPI_StreamOpen
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamClose($hStream)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamClose", "hwnd", $hStream)
	If @error Then Return SetError(-1, @error, 0)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, False)
	Return True
EndFunc   ;==>_midiAPI_StreamClose

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamOpen
; Description ...:  Opens a MIDI stream for output.
; Syntax.........: _midiAPI_StreamOpen($iDeviceID [, $pCallBk = 0 [, $iInst = 0 [, $iFlags = $CALLBACK_NULL]]] )
; Parameters ....: $iDeviceID - Identifier of the MIDI out device.
;                  $pCallBk   - Pointer to a callback function, or a handle of a window
;                  $iInst     - User "instance id" passed to the callback function.
;                               This parameter is not used with window callback functions
;                  $iFlags    - Callback flag for opening the device.
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_NODEVICE     - No MIDI port was found. This error occurs only when the mapper is opened.
;                  |$MMSYSERR_ALLOCATED   - The specified resource is already allocated.
;                  |$MMSYSERR_BADDEVICEID - The specified device identifier is out of range.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: By default, the device is opened in paused mode.
;                  $iDeviceID is an integer from: 0 to (_midiAPI_InGetNumDevs() - 1) or the $MIDI_MAPPER constant.
;                  $iFlags values can be:
;                       $CALLBACK_NULL     - $pCallBk must be 0
;                       $CALLBACK_EVENT    - $pCallBk is an event handle. (Out of this UDF Scope). Lookup CreateEventW() for
;                                            more info.
;                       $CALLBACK_FUNCTION - $pCallBk is pointer to a callback function. This method can be unstable in AutoIt.
;                                          | The callback function can be defined as:
;                                          | DllCallbackRegister("MidiOutProc", "none", "hwnd;uint;dword_ptr;dword_ptr;dword_ptr")
;                                          | The callback function has five parameters
;                                              - $hMidiIn - Handle to the MIDI input device
;                                              - $iMsg    - The $MOM_* midi message
;                                              - $iInst   - The user data specified when calling _midiAPI_InOpen
;                                              - $iParam1 - Message parameter.
;                                              - $iParam2 - Message parameter.(Usually reserved)
;                       $CALLBACK_WINDOW   - $pCallBk is a window handle
;                                          | $MM_MIM_* messages can be registered GUIRegisterMsg()
;                                          | The callback function has four parameters
;                                              - $hWnd   - The window handle
;                                              - $iMsg    - The $MM_MOM_* midi message
;                                              - $wParam  - The midi device handle
;                                              - $lParam  - Message parameter.
; Related .......: _midiAPI_OutGetNumDevs, _midOutGetDevCaps
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamOpen($iDeviceID, $pCallBk = 0, $iInst = 0, $iFlags = $CALLBACK_NULL)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamOpen", _
			"hwnd*", 0, "int*", $iDeviceID, "dword", 1, "dword_ptr", $pCallBk, "int", $iInst, "int", $iFlags)
	If @error Then Return SetError(-1, @error, 0)
	If $aRes[0] <> $MMSYSERR_NOERROR Then Return SetError($aRes[0], 0, 0)
	Return $aRes[1]
EndFunc   ;==>_midiAPI_StreamOpen

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamOut
; Description ...: Plays or queues a stream (buffer) of MIDI data to a MIDI output device
; Syntax.........: _midiAPI_StreamOut($hStream, $pMidiHdr [, $iMidiHdrSz = Default])
; Parameters ....: $hStream    - Stream handle
;                  $pMidiHdr   - Pointer to a midi header structure
;                  $iMidiHdrSz - Size of the midi header structure (x86 = 72 bytes, x64 = 120 bytes)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MIDIERR_STILLPLAYING - Buffer is still in the queue.
;                  |$MIDIERR_UNPREPARED   - Buffer has not been prepared.
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
;                  |$MMSYSERR_NOMEM       - The system is unable to allocate or lock memory.
; Author ........: MattyD
; Modified.......:
; Remarks .......: The buffer must be prepared using the midiOutPrepareHeader function before it is passed to _midiAPI_StreamOut.
;                  Iy must be smaller than 64K.
;                  Because _midiAPI_StreamOpen puts the device in paused mode, you must call _midiAPI_StreamRestart before you can
;                  start the playback.
;                  The buffer pointed to by the MIDIHDR structure contains one or more MIDI events, each of which is defined by
;                  a MIDIEVENT structure.
; Related .......: _midiAPI_OutPrepareHeader
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamOut($hStream, $pMidiHdr, $iMidiHdrSz = Default)
	If $iMidiHdrSz = Default Then
		$iMidiHdrSz = 72
		If @AutoItX64 Then $iMidiHdrSz = 120
	EndIf
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamOut", _
			"hwnd", $hStream, "ptr", $pMidiHdr, "uint", $iMidiHdrSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_StreamOut

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamPause
; Description ...: Pauses playback of a specified MIDI stream.
; Syntax.........: _midiAPI_StreamPause($hStream)
; Parameters ....: $hStream    - Stream handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: To resume playback from the current position, use the _midiAPI_StreamRestart function.
; Related .......: _midiAPI_StreamRestart, _midiAPI_StreamStop
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamPause($hStream)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamPause", "hwnd", $hStream)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_StreamPause

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamPosition
; Description ...: Retrieves the current position in a MIDI stream
; Syntax.........: _midiAPI_StreamPosition($hStream, $pMMTime [, $iMMTimeSz = 8])
; Parameters ....: $hStream    - Stream handle
;                  $pMMTime    - Pointer to a MMTime structure
;                  $iMMTimeSz  - Size of the MMTime struct: (usually 8 bytes; 12 when specifying smpte)
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: One of the following tags can be used to create the struct:
;                  $tag_mmtime        - wType    $TIME_* type (except $TIME_SMPTE)
;                                     | dwData     corresponding data
;                  $tag_mmtime_smpte  - wType    $TIME_SMPTE
;                                     | hour     Hours
;                                     | min      Minutes
;                                     | sec      Seconds
;                                     | frame    Frames
;                                     | fps      Frames per second (24, 25, 29 (30 drop), or 30)
;                  If the time type is not supported, wType will be populated with an alternative format.
; Related .......: _midiAPI_StreamProperty
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamPosition($hStream, $pMMTime, $iMMTimeSz = 8)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamPosition", _
			"hwnd", $hStream, "ptr", $pMMTime, "uint", $iMMTimeSz)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_StreamPosition

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamProperty
; Description ...: Sets or retrieves properties of a MIDI data stream associated with a MIDI output device.
; Syntax.........: _midiAPI_StreamProperty($hStream, $iProperty, $pData)
; Parameters ....: $hStream    - Stream handle
;                  $iProperty  - Specifies the property, and what action to perform on it (get or set).
;                  $pData      - Pointer to property struct
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
;                  |$MMSYSERR_INVALPARAM  - Pointer or structure is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: For $iProperty, use BitOr to combine an action value with a property value:
;				   Action values:   $MIDIPROP_SET, $MIDIPROP_GET
;				   Property Values: $MIDIPROP_TIMEDIV, $MIDIPROP_TEMPO
;                  The corresponding tags can be used to create and fill a property struct.
;                                - $tag_midiAPI_proptempo
;                                     cbStruct  - size of the struct (set to 8)
;                                     dwTempo   - in microseconds per quarter note.
;                                - $tag_midiAPI_proptimediv
;                                     cbStruct  - size of the struct (set to 8)
;                                     dwTimeDiv - Midi time division.
;                  The timediv property can be set only when the device is stopped.
;                  The tempo is honoured only if the time division for the stream is specified in quarter note format.
; Related .......: _midiAPI_StreamPosition
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamProperty($hStream, $iProperty, $pData)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamProperty", _
			"hwnd", $hStream, "ptr", $pData, "dword", $iProperty)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_StreamProperty

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamRestart
; Description ...: Restarts a paused MIDI stream.
; Syntax.........: _midiAPI_StreamRestart($hStream)
; Parameters ....: $hStream    - Stream handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: To resume playback from the current position, use the _midiAPI_StreamRestart function.
; Related .......: _midiAPI_StreamOut, _midiAPI_StreamPause, _midiAPI_StreamStop
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamRestart($hStream)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamRestart", "hwnd", $hStream)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_StreamRestart

; #FUNCTION# ====================================================================================================================
; Name...........: _midiAPI_StreamStop
; Description ...: Turns off all notes on all MIDI channels for the specified MIDI output device.
; Syntax.........: _midiAPI_StreamStop($hStream)
; Parameters ....: $hStream    - Stream handle
; Return values .: Success: True, @error = 0
;                  Failure: False, @error <> 0
;                  @error: -1             - Dll call failure. @extended set to DllCall() error
;                  |$MMSYSERR_INVALHANDLE - Device handle is invalid.
; Author ........: MattyD
; Modified.......:
; Remarks .......: When called, any pending system-exclusive or stream output buffers are returned to the callback mechanism
;                  and the MHDR_DONE bit is set in the dwFlags member of the MIDIHDR structure.
;                  While _midiAPI_OutReset turns off all notes, midiStreamStop turns off only those notes that have been turned on
;                  by a MIDI note-on message.
; Related .......: _midiAPI_StreamOut, _midiAPI_OutReset
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midiAPI_StreamStop($hStream)
	Local $aRes = DllCall($__g_hWinMMDll, "int", "midiStreamStop", "hwnd", $hStream)
	If @error Then Return SetError(-1, @error, False)
	Return SetError($aRes[0], 0, $aRes[0] = $MMSYSERR_NOERROR)
EndFunc   ;==>_midiAPI_StreamStop
