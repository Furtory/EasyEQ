#include-once
#include "..\Midi\API\midiAPI.au3"
#include "..\Midi\UDF\midiConstants.au3"

; #INDEX# =======================================================================================================================
; Title .........: Midi UDF
; AutoIt Version : 3.3.16.0
; Description ...: Provides an interface for sending and recieving common midi messages.
;                  Changed: Window messages handling moved to this function so midi can be stopped and restarted again
; Author(s) .....: MattyD
; Dll ...........: winmm.dll
; ===============================================================================================================================

; #CONSTANTS# ===================================================================================================================
Global Const $__INPUTQUEUE_MAX = 1024
Global Const $__INPUTQUEUE_TIMEOUT = 2000
Global Const $__INPUTBUFFER_SIZE = 128
Global Const $__INPUTBUFFER_COUNT = 10
Global Const $__DEV_OPENCLOSE_TIMEOUT = 1000
; ===============================================================================================================================

; #VARIABLES# ===================================================================================================================
Global $__g_avBuffers[0][4]
Global $__g_adInputQueue[$__INPUTQUEUE_MAX][4]
Global $__g_adLongInputQueue[$__INPUTQUEUE_MAX][4]
Global $__g_ahInputTimers[0][2]
Global $__g_avDevStates[0][2]
;~ Global $__g_sDebugFunc
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;_midi_CloseInput
;_midi_CloseOutput
;_midi_DecrementNRPN
;_midi_DecrementRPN
;_midi_EnumInputs
;_midi_EnumOutputs
;_midi_GetInputName
;_midi_GetOutputName
;_midi_IncrementNRPN
;_midi_IncrementRPN
;_midi_OpenInput
;_midi_OpenOutput
;_midi_PackAddress
;_midi_PackSize
;_midi_Read14bitCtrlChange
;_midi_ReadIDReply
;_midi_ReadMsg
;_midi_ReadPatchChange
;_midi_ReadRolandDT1
;_midi_ReadRPN
;_midi_ReadSysEx
;_midi_ReadXGDataDump
;_midi_ReadXGParamChg
;_midi_RegDebugFunc
;_midi_SelectPatch
;_midi_Send14bitCtrlChange
;_midi_SendAllNotesOff
;_midi_SendAllSoundOff
;_midi_SendChannelPressure
;_midi_SendControlChange
;_midi_SendIDRequest
;_midi_SendMsg
;_midi_SendNoteOff
;_midi_SendNoteOn
;_midi_SendPitchBend
;_midi_SendPolyKeyPressure
;_midi_SendProgramChange
;_midi_SendResetControllers
;_midi_SendRolandDT1
;_midi_SendRolandGSReset
;_midi_SendRolandRQ1
;_midi_SendSysEx
;_midi_SendXGDumpRequest
;_midi_SendXGParamChange
;_midi_SendXGParamRequest
;_midi_SendXGSystemOn
;_midi_SetADSR_Attack
;_midi_SetADSR_Decay
;_midi_SetADSR_Release
;_midi_SetAftertouchDest
;_midi_SetChanCourseTuning
;_midi_SetChanFineTuning
;_midi_SetChorusDepth
;_midi_SetChorusFeedback
;_midi_SetChorusRate
;_midi_SetChorusReverb
;_midi_SetChorusType
;_midi_SetControllerDest
;_midi_SetDrumChorus
;_midi_SetDrumLevel
;_midi_SetDrumPan
;_midi_SetDrumPitch
;_midi_SetDrumProperties
;_midi_SetDrumReverb
;_midi_SetFilterCutoff
;_midi_SetFilterResonance
;_midi_SetGMMode
;_midi_SetLocalControl
;_midi_SetMidiMode
;_midi_SetModDepthRange
;_midi_SetMstrBalance
;_midi_SetMstrCourseTuning
;_midi_SetMstrFineTuning
;_midi_SetMstrVolume
;_midi_SetNRPN
;_midi_SetOctiveTuning
;_midi_SetPitchBendSens
;_midi_SetReverbTime
;_midi_SetReverbType
;_midi_SetRPN
;_midi_SetVibratoDelay
;_midi_SetVibratoDepth
;_midi_SetVibratoRate
;_midi_Shutdown
;_midi_Startup
;_midi_UnpackAddress
;_midi_UnpackSize
; ===============================================================================================================================

; #INTERNAL_USE_ONLY# ===========================================================================================================
;__midi_AddBuffer
;__midi_BuildGblParCtrlMsg
;__midi_BuildShortMsg
;__midi_BuildSysEx_Kawai
;__midi_BuildSysEx_Roland
;__midi_BuildSysEx_Uvsl
;__midi_BuildSysEx_YamahaXG
;__midi_CleanQueue
;__midi_CleanQueues
;__midi_CreateBuffer
;__midi_Debug
;__midi_FillBuffer
;__midi_FreeBuffer
;__midi_GetBufferIdx
;__midi_GetLongMsg
;__midi_GetMsg
;__midi_InspectSysEx_Kawai
;__midi_InspectSysEx_Roland
;__midi_InspectSysEx_Uvsl
;__midi_InspectSysEx_YamahaXG
;__midi_StartInput
;__MM_MIM_DATA
;__MM_MIM_LONGDATA
;__MM_STATE_CHANGE
; ===============================================================================================================================

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_AddBuffer
; Description ...: Creates a registers generic buffer.
; Syntax ........: __midi_AddBuffer($hDevice, $iSize)
; Parameters ....: $hDevice - Specifies a midi device handle that the buffer is intended to be passed to.
;                  $iSize - Specifies the size of the buffer to create.
; Return values .: The index in the register where the buffer is stored.
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: __midi_FillBuffer
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_AddBuffer($hDevice, $iSize)
	Local $iIndex, $pMidiHdr, $tMidiHdr, $tBuffer

	For $i = 0 To UBound($__g_avBuffers) - 1
		If Not $__g_avBuffers[$i][0] Then ExitLoop
	Next
	If $i = UBound($__g_avBuffers) Then ReDim $__g_avBuffers[$i + 5][4]
	$iIndex = $i

	__midi_CreateBuffer($pMidiHdr, $tMidiHdr, $tBuffer, $iSize)
	$__g_avBuffers[$iIndex][0] = $hDevice
	$__g_avBuffers[$iIndex][1] = $pMidiHdr
	$__g_avBuffers[$iIndex][2] = $tMidiHdr
	$__g_avBuffers[$iIndex][3] = $tBuffer

	Return $iIndex
EndFunc   ;==>__midi_AddBuffer

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_BuildGblParCtrlMsg
; Description ...:
; Syntax ........: __midi_BuildGblParCtrlMsg($aSlotPaths, $dParam, $dValue[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $aiSlotPaths - an array of slot paths. Value may be 0.
;                  $dParam - The parameter to update.
;                  $dValue - The new value of the parameter.
;                  $dDeviceID - A binary representation of a device ID.
; Return values .: A binary "Global Parameter Control" SysEx message.
; Author ........: MattyD
; Modified ......:
; Remarks .......: Slot paths must be represented as follows:
;                  $aiSlotPaths[Index][0] = MSB
;                  $aiSlotPaths[Index][1] = LSB
;                  $aiSlotPaths may be 0 to control top-level parameters.
;                  The length of binary parameters must be correct, do not use an integer representation.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_BuildGblParCtrlMsg($aiSlotPaths, $dParam, $dValue, $dDeviceID = $DEVID_BROADCAST)
	Local $tBuffer, $tBody, $iBodyLen, $pBody, $tagBody
	Local $iSlotPathCnt, $iParamLen, $iValueLen

	$iSlotPathCnt = UBound($aiSlotPaths)
	$iParamLen = BinaryLen($dParam)
	$iValueLen = BinaryLen($dValue)
	$iBodyLen = 2 * $iSlotPathCnt + $iParamLen + $iValueLen + 3

	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_DEVCTRL, 5, $iBodyLen)

	$tagBody = StringFormat("byte[3];byte slots[%d]; byte param[%d];byte value[%d]", 2 * $iSlotPathCnt, $iParamLen, $iValueLen)
	$tBody = DllStructCreate($tagBody, $pBody)
	DllStructSetData($tBody, 1, $iSlotPathCnt, 1)
	DllStructSetData($tBody, 1, $iParamLen, 2)
	DllStructSetData($tBody, 1, $iValueLen, 3)

	For $i = 0 To $iSlotPathCnt - 1
		DllStructSetData($tBody, "slots", $aiSlotPaths[$i][0], 2 * $i + 1)
		DllStructSetData($tBody, "slots", $aiSlotPaths[$i][1], 2 * $i + 2)
	Next

	DllStructSetData($tBody, "param", $dParam)
	DllStructSetData($tBody, "value", $dValue)

	Return DllStructGetData($tBuffer, 1)
EndFunc   ;==>__midi_BuildGblParCtrlMsg

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_BuildShortMsg
; Description ...: Prepares a short midi message for use with MidiOutShortMsg
; Syntax ........: __midi_BuildShortMsg($iMsg, $iChannel[, $iParam1 = 0[, $iParam2 = Default]])
; Parameters ....: $iMsg - A status byte with the channel bits omitted. ($MSG_* Constant)
;                  $iChannel - The 1-based midi channel, or basic channel
;                  $iParam1 - The first parameter the midi message if required.
;                  $iParam2 - The second parameter the midi message if required.
; Return values .: Success: An integer that encapsulates a short midi message.
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $iParam1 represents the full 14bit value for a Pitch Bend message, not the first data byte.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_BuildShortMsg($iMsg, $iChannel, $iParam1 = 0, $iParam2 = 0)
	Local Static $tMsg = DllStructCreate("byte[3]")

	If $iChannel < 1 Or $iChannel > 16 Then Return False
	If BitAND($iMsg, 0xF0) = $MSG_PC Then $iParam1 -= 1

	DllStructSetData($tMsg, 1, BitOR(BitAND($iMsg, 0xF0), $iChannel - 1), 1)
	DllStructSetData($tMsg, 1, BitAND(0x7F, $iParam1), 2)
	If BitAND($iMsg, 0xF0) = $MSG_BEND Then
		If $iParam1 < 0 Or $iParam1 > 0x3FFF Then Return False
		DllStructSetData($tMsg, 1, BitAND(0x7F, BitShift($iParam1, 7)), 3)
	Else
		If $iParam1 < 0 Or $iParam1 > 0x7F Then Return False
		If $iParam2 < 0 Or $iParam2 > 0x7F Then Return False
		DllStructSetData($tMsg, 1, BitAND(0x7F, $iParam2), 3)
	EndIf

	Return Int(DllStructGetData($tMsg, 1))
EndFunc   ;==>__midi_BuildShortMsg

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_BuildSysEx_Kawai
; Description ...: Prepares memory for the construction of a Kawai SysEx message.
; Syntax ........: __midi_BuildSysEx_Kawai(Byref $tBuffer, $iChannel, $iGroup, $iMachine, $iFunction[, $iBodyLen = 0])
; Parameters ....: $tBuffer - [out] A buffer containg the SysEx message
;                  $iChannel - The 0 based basic channel of the device (deviceID). (0 - 0x0F)
;                  $iGroup - Represents the device type (0 - 127)
;                  $iMachine - Represents the model of the device (0 - 127)
;                  $iFunction - A Kawai function code
;                  $iBodyLen  - The length of the body segment of the message.
; Return values .: Success: A pointer to the body segment of the SysEx struct.
; Author ........: MattyD
; Modified ......:
; Remarks .......: The entire SysEx message is held in the first element of the $tBuffer struct. The binary message can be
;                  obtained by calling DllStructGetData.
;                  Only one SysEx message can be prepared at a time. Each call to __midi_BuildSysEx_Kawai will free the previously
;                  created buffer.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_BuildSysEx_Kawai(ByRef $tBuffer, $iChannel, $iGroup, $iMachine, $iFunction, $iBodyLen = 0)
	Local Static $tSysEx
	Local $tagSysEx, $pSysEx, $iSysExLen

	$tagSysEx = StringFormat( _
			"byte SOX;byte MID;byte CHAN;byte FUNC;byte GROUP;byte MACH;byte BODY[%d];byte EOX", _
			$iBodyLen)
	If Not $iBodyLen Then $tagSysEx = StringReplace($tagSysEx, "byte BODY[0];", "")

	$tSysEx = DllStructCreate($tagSysEx)
	DllStructSetData($tSysEx, "SOX", Binary("0xF0"))
	DllStructSetData($tSysEx, "MID", $MID_KAWAI)
	DllStructSetData($tSysEx, "CHAN", $iChannel)
	DllStructSetData($tSysEx, "FUNC", $iFunction)
	DllStructSetData($tSysEx, "GROUP", $iGroup)
	DllStructSetData($tSysEx, "MACH", $iMachine)
	DllStructSetData($tSysEx, "EOX", Binary("0xF7"))

	$pSysEx = DllStructGetPtr($tSysEx)
	$iSysExLen = DllStructGetSize($tSysEx)
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", DllStructGetSize($tSysEx)), $pSysEx)

	Return Ptr(Number($pSysEx) + ($iSysExLen - ($iBodyLen + 1)))
EndFunc   ;==>__midi_BuildSysEx_Kawai

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_BuildSysEx_Roland
; Description ...: Prepares memory for the construction of a Roland SysEx message.
; Syntax ........: __midi_BuildSysEx_Roland(Byref $tBuffer, $iDeviceID, $dModel, $dCommand[, $iBodyLen = 0])
; Parameters ....: $tBuffer - [out] A buffer containg the SysEx message
;                  $iDeviceID - A device ID. (typically 0x10)
;                  $dModel - a binary repesentation of the model. (typically 4 bytes)
;                  $dCommand - a binary represetaion of the command to send (typically 1 byte)
;                  $iBodyLen  - The length of the body segment of the message.
; Return values .: Success: A pointer to the body segment of the SysEx struct.
; Author ........: MattyD
; Modified ......:
; Remarks .......: The length of binary parameters must be correct, do not use an integer representation.
;                  After calling this function you must fill the body segment of the message. The location of this is returned
;                  by the function.
;                  The entire SysEx message is held in the first element of the $tBuffer struct. The binary message can be
;                  obtained by calling DllStructGetData.
;                  Only one SysEx message can be prepared at a time. Each call to __midi_BuildSysEx_Roland will free the previously
;                  created buffer.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_BuildSysEx_Roland(ByRef $tBuffer, $iDeviceID, $dModel, $dCommand, $iBodyLen = 0)
	Local Static $tSysEx
	Local $tagSysEx, $pSysEx, $iSysExLen
	Local $iMDLLen = BinaryLen($dModel), $iCMDLen = BinaryLen($dCommand)

	$tagSysEx = StringFormat( _
			"byte SOX;byte MID;byte DEV;byte MODEL[%d];byte CMD[%d];byte BODY[%d];byte EOX", _
			$iMDLLen, $iCMDLen, $iBodyLen)
	If Not $iBodyLen Then $tagSysEx = StringReplace($tagSysEx, "byte BODY[0];", "")

	$tSysEx = DllStructCreate($tagSysEx)
	DllStructSetData($tSysEx, "SOX", Binary("0xF0"))
	DllStructSetData($tSysEx, "MID", $MID_ROLAND)
	DllStructSetData($tSysEx, "DEV", $iDeviceID)
	DllStructSetData($tSysEx, "MODEL", $dModel)
	DllStructSetData($tSysEx, "CMD", $dCommand)
	DllStructSetData($tSysEx, "EOX", Binary("0xF7"))

	$pSysEx = DllStructGetPtr($tSysEx)
	$iSysExLen = DllStructGetSize($tSysEx)
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", DllStructGetSize($tSysEx)), $pSysEx)

	Return Ptr(Number($pSysEx) + ($iSysExLen - ($iBodyLen + 1)))
EndFunc   ;==>__midi_BuildSysEx_Roland

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_BuildSysEx_Uvsl
; Description ...: Prepares memory for the construction of a Universal SysEx message.
; Syntax ........: __midi_BuildSysEx_Uvsl(Byref $tBuffer, $dMID, $dDeviceID, $iSubID1, $iSubID2[, $iBodyLen = 0])
; Parameters ....: $tBuffer - [out] A buffer containg the SysEx message
;                  $dUID - A universal ID ($UID_REALTIME or $UID_NONREALTIME).
;                  $iDeviceID - A device ID. $DEVID_BROADCAST may be used.
;                  $iSubID1 - The first Sub ID of the message.
;                  $iSubID2 - The Second Sub ID of the message.
;                  $iBodyLen - TThe length of the body segment of the message.
; Return values .: Success: A pointer to the body segment of the SysEx struct.
; Author ........: MattyD
; Modified ......:
; Remarks .......: The length of $dUID must be correct, do not use an integer representation.
;                  After calling this function you must fill the body segment of the message. The location of this is returned
;                  by the function.
;                  The entire SysEx message is held in the first element of the $tBuffer struct. The binary message can be
;                  obtained by calling DllStructGetData.
;                  Only one SysEx message can be prepared at a time. Each call to __midi_BuildSysEx_Uvsl will free the previously
;                  created buffer.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_BuildSysEx_Uvsl(ByRef $tBuffer, $dUID, $iDeviceID, $iSubID1, $iSubID2, $iBodyLen = 0)
	Local Static $tSysEx
	Local $tagSysEx, $pSysEx, $iSysExLen

	$tagSysEx = StringFormat("byte SOX;byte UID;byte DEVID;byte SUBIDS[2];byte BODY[%d];byte EOX", $iBodyLen)
	If Not $iBodyLen Then $tagSysEx = StringReplace($tagSysEx, "byte BODY[0];", "")

	$tSysEx = DllStructCreate($tagSysEx)
	DllStructSetData($tSysEx, "SOX", Binary("0xF0"))
	DllStructSetData($tSysEx, "UID", $dUID)
	DllStructSetData($tSysEx, "DEVID", $iDeviceID)
	DllStructSetData($tSysEx, "SUBIDS", $iSubID1, 1)
	DllStructSetData($tSysEx, "SUBIDS", $iSubID2, 2)
	DllStructSetData($tSysEx, "EOX", Binary("0xF7"))

	$pSysEx = DllStructGetPtr($tSysEx)
	$iSysExLen = DllStructGetSize($tSysEx)
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", DllStructGetSize($tSysEx)), $pSysEx)

	Return Ptr(Number($pSysEx) + ($iSysExLen - ($iBodyLen + 1)))
EndFunc   ;==>__midi_BuildSysEx_Uvsl

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_BuildSysEx_YamahaXG
; Description ...: Prepares memory for the construction of a Roland SysEx message.
; Syntax ........: __midi_BuildSysEx_YamahaXG(ByRef $tBuffer, $iDeviceNum, $iCommand, $dAddress[, $iDataLen = 0])
; Parameters ....: $tBuffer - [out] A buffer containg the SysEx message
;                  $iDeviceNum - The zero based 4-bit yamaha device number (0 - 0x0F)
;                  $iCommand - $YAMXG_PARAM_REQ, $YAMXG_DUMP_REQ or $YAMXG_PARAM_CHG
;                  $dAddress - The 3 byte address to query from, or write to.
;                  $iDataLen  - The size of the data if writing.
; Return values .: Success: A pointer to the body segment of the SysEx struct.
; Author ........: MattyD
; Modified ......:
; Remarks .......: The length of binary parameters must be correct, do not use an integer representation.
;                  If building a parameter change message you must fill the data segment. The location of this is returned
;                  by the function.
;                  The entire SysEx message is held in the first element of the $tBuffer struct. The binary message can be
;                  obtained by calling DllStructGetData.
;                  Only one SysEx message can be prepared at a time. Each call to __midi_BuildSysEx_Roland will free the previously
;                  created buffer.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_BuildSysEx_YamahaXG(ByRef $tBuffer, $iDeviceNum, $iCommand, $dAddress, $iDataLen = 0)
	Local Static $tSysEx
	Local $tagSysEx, $pSysEx, $iSysExLen
	Local $dDeviceID

	$dDeviceID = Binary(Chr(BitOR($iCommand, $iDeviceNum)))
	$tagSysEx = StringFormat( _
			"byte SOX;byte MID;byte DEVID;byte MODEL;byte ADDRESS[3];byte DATA[%d];byte EOX", _
			$iDataLen)
	If Not $iDataLen Then $tagSysEx = StringReplace($tagSysEx, "byte DATA[0];", "")

	$tSysEx = DllStructCreate($tagSysEx)
	DllStructSetData($tSysEx, "SOX", Binary("0xF0"))
	DllStructSetData($tSysEx, "MID", $MID_YAMAHA)
	DllStructSetData($tSysEx, "DEVID", $dDeviceID)
	DllStructSetData($tSysEx, "MODEL", $MODEL_YMHA_XG)
	DllStructSetData($tSysEx, "ADDRESS", $dAddress)
	DllStructSetData($tSysEx, "EOX", Binary("0xF7"))

	$pSysEx = DllStructGetPtr($tSysEx)
	$iSysExLen = DllStructGetSize($tSysEx)
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", DllStructGetSize($tSysEx)), $pSysEx)

	Return Ptr(Number($pSysEx) + ($iSysExLen - ($iDataLen + 1)))
EndFunc   ;==>__midi_BuildSysEx_YamahaXG

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_CleanQueue
; Description ...: Purges unhandled midi messages from an input queue.
; Syntax ........: __midi_CleanQueue(Byref $adQueue)
; Parameters ....: $adQueue - The queue to be inspected.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_CleanQueue(ByRef $adQueue)
	Local $iElaped

	For $i = 0 To UBound($__g_ahInputTimers) - 1
		$iElaped = TimerDiff($__g_ahInputTimers[$i][1])
		For $j = 0 To UBound($adQueue) - 1
			If $adQueue[$j][3] Then ContinueLoop
			If $__g_ahInputTimers[$i][0] = $adQueue[$j][0] Then
				If $iElaped - $adQueue[$j][2] > $__INPUTQUEUE_TIMEOUT Then
					$adQueue[$j][3] = True
				EndIf
			EndIf
		Next
	Next
EndFunc   ;==>__midi_CleanQueue

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_CleanQueues
; Description ...: Purges unhandled midi messages from the input queues.
; Syntax ........: __midi_CleanQueues()
; Parameters ....: None
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_CleanQueues()
	__midi_CleanQueue($__g_adInputQueue)
	__midi_CleanQueue($__g_adLongInputQueue)
EndFunc   ;==>__midi_CleanQueues

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_CreateBuffer
; Description ...: Creates a generic buffer for use with midi devices.
; Syntax ........: __midi_CreateBuffer(Byref $pMidiHdr, Byref $tMidiHdr, Byref $tBuffer, $iSize)
; Parameters ....: $pMidiHdr - [out] a pointer to the midi header structure.
;                  $tMidiHdr - [out] the midi header structure.
;                  $tBuffer - [out] the buffer
;                  $iSize - Specifies the size of the buffer to create.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: __midi_AddBuffer
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_CreateBuffer(ByRef $pMidiHdr, ByRef $tMidiHdr, ByRef $tBuffer, $iSize)
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iSize))
	$tMidiHdr = DllStructCreate($tag_midihdr)
	$pMidiHdr = DllStructGetPtr($tMidiHdr)
	DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
	DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))
EndFunc   ;==>__midi_CreateBuffer

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_Debug
; Description ...: Fowards debug messages to a user defined function.
; Syntax ........: __midi_Debug($hDevice, $iFlag, $vMsg)
; Parameters ....: $hDevice - A midi device handle.
;                  $iFlag - $MIDI_DBG_IO_IN or $MIDI_DBG_IO_OUT.
;                  $vMsg - A midi message.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
;~ Func __midi_Debug($hDevice, $iFlag, $vMsg)
;~ 	Local Static $tMsg = DllStructCreate("byte[3]")
;~ 	If $__g_sDebugFunc Then
;~ 		If IsInt($vMsg) Then
;~ 			DllStructSetData($tMsg, 1, $vMsg)
;~ 			$vMsg = DllStructGetData($tMsg, 1)
;~ 		EndIf
;~ 		Call($__g_sDebugFunc, $hDevice, $iFlag, $vMsg)
;~ 		If @error Then $__g_sDebugFunc = ""
;~ 	EndIf
;~ EndFunc   ;==>__midi_Debug

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_FillBuffer
; Description ...: Fills a buffer in the buffer register with specified data.
; Syntax ........: __midi_FillBuffer($iIndex, $dData)
; Parameters ....: $iIndex - The index of the buffer to fill.
;                  $dData - The binary data to place in the buffer
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: __midi_AddBuffer
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_FillBuffer($iIndex, $dData)
	Local $tBuffer, $tData, $pData
	Local $iBuffLen, $iBinLen, $iRecorded

	$tBuffer = $__g_avBuffers[$iIndex][3]

	$iBuffLen = DllStructGetSize($__g_avBuffers[$iIndex][3])
	$iRecorded = DllStructGetData($__g_avBuffers[$iIndex][2], "dwBytesRecorded")
	$iBinLen = BinaryLen($dData)
	If $iRecorded + $iBinLen > $iBuffLen Then Return False

	$pData = DllStructGetPtr($tBuffer) + $iRecorded
	$tData = DllStructCreate(StringFormat("byte[%d]", $iBinLen), $pData)

	DllStructSetData($tData, 1, $dData)
	DllStructSetData($__g_avBuffers[$iIndex][2], "dwBytesRecorded", $iRecorded + $iBinLen)

	Return True
EndFunc   ;==>__midi_FillBuffer

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_FreeBuffer
; Description ...: Frees a buffer that is stored in the buffer register.
; Syntax ........: __midi_FreeBuffer($iIndex)
; Parameters ....: $iIndex - The index of the buffer to free.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_FreeBuffer($iIndex)
	$__g_avBuffers[$iIndex][0] = 0
	$__g_avBuffers[$iIndex][1] = 0
	$__g_avBuffers[$iIndex][2] = 0
	$__g_avBuffers[$iIndex][3] = 0
EndFunc   ;==>__midi_FreeBuffer

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_GetBufferIdx
; Description ...: Retrieves the index of a buffer from a pointer to its midi header struct.
; Syntax ........: __midi_GetBufferIdx($pMidiHdr)
; Parameters ....: $pMidiHdr - A pointer to a midi header struct.
; Return values .: Success: The buffer's index in the register.
;                  Failure: -1
; Author ........: MattyD
; Modified ......:
; Remarks .......: For use with callback functions.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_GetBufferIdx($pMidiHdr)
	For $i = 0 To UBound($__g_avBuffers) - 1
		If $__g_avBuffers[$i][1] = $pMidiHdr Then ExitLoop
	Next
	If $i = UBound($__g_avBuffers) Then Return -1
	Return $i
EndFunc   ;==>__midi_GetBufferIdx

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_GetLongMsg
; Description ...: Retrieves a system exclusive message from the input queue (if available).
; Syntax ........: __midi_GetLongMsg($hDevice, Byref $dData)
; Parameters ....: $hDevice - A handle to a midi input device.
;                  $dData - [out] A system exclusive message.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: It is possible for a SysEx message to span across multiple buffers. In this case __midi_GetLongMsg must be
;                  be called multiple times to construct a complete SysEx message.
;                  The messages returned from this function are not validated, and should be checked downstream.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_GetLongMsg($hDevice, ByRef $dData)
	Local $iIndex = -1, $iTS

	For $i = 0 To UBound($__g_adLongInputQueue) - 1
		If $__g_adLongInputQueue[$i][0] <> $hDevice Then ContinueLoop
		If Not $__g_adLongInputQueue[$i][1] Then ContinueLoop
		If Not $__g_adLongInputQueue[$i][3] Then
			If Not $iTS Or $iTS > $__g_adLongInputQueue[$i][2] Then
				$iIndex = $i
				$iTS = $__g_adLongInputQueue[$iIndex][2]
			EndIf
		EndIf
	Next

	If $iIndex = -1 Then Return False

	$dData = $__g_adLongInputQueue[$iIndex][1]
	$__g_adLongInputQueue[$iIndex][3] = True

	Return True
EndFunc   ;==>__midi_GetLongMsg

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_GetMsg
; Description ...: Retrives the location of the next midi message for a device.
; Syntax ........: __midi_GetMsg($hDevice[, $bInclLong = False])
; Parameters ....: $hDevice - A midi input device handle.
;                  $bInclLong - If False, only short messages are retrieved
; Return values .: Success: The index of an input queue where the message is located.
;                  @extended: 0 - The index is for the short message queue
;                             1 - The index is for the long message queue
;                  Failure: -1
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_GetMsg($hDevice, $bInclLong = False)
	Local $iIndex = -1, $iTS, $bLongMsg = False

	For $i = 0 To UBound($__g_adInputQueue) - 1
		If $__g_adInputQueue[$i][0] <> $hDevice Then ContinueLoop
		If Not $__g_adInputQueue[$i][3] Then
			If Not $iTS Or $iTS > $__g_adInputQueue[$i][2] Then
				$iIndex = $i
				$iTS = $__g_adInputQueue[$iIndex][2]
			EndIf
		EndIf
	Next

	If Not $bInclLong Then Return $iIndex

	For $i = 0 To UBound($__g_adLongInputQueue) - 1
		If $__g_adLongInputQueue[$i][0] <> $hDevice Then ContinueLoop
		If Not $__g_adLongInputQueue[$i][3] Then
			If Not $iTS Or $iTS > $__g_adLongInputQueue[$i][2] Then
				$bLongMsg = True
				$iIndex = $i
				$iTS = $__g_adLongInputQueue[$iIndex][2]
			EndIf
		EndIf
	Next

	Return SetExtended(Int($bLongMsg), $iIndex)
EndFunc   ;==>__midi_GetMsg

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_InspectSysEx_Kawai
; Description ...: Deconstructs a universal system exclusive message header.
; Syntax ........: __midi_InspectSysEx_Kawai($dSysEx)
; Parameters ....: $dSysEx - A system exclusive message
; Return values .: Success: A 6 element array of binary variants:
;                  $adMsg[0] - The manufacurer's ID, $MID_KAWAI (1 byte)
;                  $adMsg[1] - The 0 based basic channel of the device (device ID) (1 byte)
;                  $adMsg[2] - The device type (1 byte)
;                  $adMsg[4] - The device model (1 byte)
;                  $adMsg[3] - A function code (1 byte)
;                  $adMsg[5] - Contains the data segment of the message for further deconstruction.
;                  Failure: An empty 6 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_InspectSysEx_Kawai($dSysEx)
	Local $tBuffer, $tSysEx, $iBinLen = BinaryLen($dSysEx)
	Local $tagSysEx, $dMID
	Local $iDataLen
	Local $adMsg[6]

	If $iBinLen < 7 Then Return SetError(1, 0, $adMsg)

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", BinaryLen($dSysEx)))
	DllStructSetData($tBuffer, 1, $dSysEx)
	If DllStructGetData($tBuffer, 1, 1) <> Binary("0xF0") Then Return SetError(1, 0, $adMsg)
	If DllStructGetData($tBuffer, 1, $iBinLen) <> Binary("0xF7") Then Return SetError(1, 0, $adMsg)
	$dMID = DllStructGetData($tBuffer, 1, 2)
	If $dMID <> $MID_KAWAI Then Return SetError(1, 0, $adMsg)

	$iDataLen = $iBinLen - 7
	$tagSysEx = StringFormat( _
			"byte SOX;byte MID[1];byte CHAN[1];byte FUNC[1];byte GROUP[1];byte MACH[1];byte BODY[%d];byte EOX", _
			$iDataLen)
	If Not $iDataLen Then $tagSysEx = StringReplace($tagSysEx, "byte DATA[0];", "")

	$tSysEx = DllStructCreate($tagSysEx, DllStructGetPtr($tBuffer))

	$adMsg[0] = $dMID
	$adMsg[1] = DllStructGetData($tSysEx, "CHAN")
	$adMsg[2] = DllStructGetData($tSysEx, "GROUP")
	$adMsg[3] = DllStructGetData($tSysEx, "MACH")
	$adMsg[4] = DllStructGetData($tSysEx, "FUNC")
	$adMsg[5] = DllStructGetData($tSysEx, "BODY")
	If @error Then $adMsg[4] = Binary("0x")

	Return $adMsg
EndFunc   ;==>__midi_InspectSysEx_Kawai

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_InspectSysEx_Roland
; Description ...: Deconstructs a Roland system exclusive message header.
; Syntax ........: __midi_InspectSysEx_Roland($dSysEx)
; Parameters ....: $dSysEx - A system exclusive message
; Return values .: Success: A 5 element array of binary variants:
;                  $adMsg[0] - The manufacurer's ID - $MID_ROLAND. (1 byte)
;                  $adMsg[1] - A device ID (1 byte)
;                  $adMsg[2] - The command ID (Length varies, typically 1 byte)
;                  $adMsg[3] - The model ID (Length varies, typically 1 - 4 bytes)
;                  $adMsg[4] - Contains the data segment of the message for further deconstruction.
;                  Failure: An empty 5 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_InspectSysEx_Roland($dSysEx)
	Local $tBuffer, $tSysEx, $iBinLen = BinaryLen($dSysEx)
	Local $tagSysEx, $dMID
	Local $iModelLen, $iCMDLen, $iDataLen
	Local $iModelOffset = 3, $iCmdOffset
	Local $adMsg[5]

	If $iBinLen < 7 Then Return SetError(1, 0, $adMsg)

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", BinaryLen($dSysEx)))
	DllStructSetData($tBuffer, 1, $dSysEx)
	If DllStructGetData($tBuffer, 1, 1) <> Binary("0xF0") Then Return SetError(1, 0, $adMsg)
	If DllStructGetData($tBuffer, 1, $iBinLen) <> Binary("0xF7") Then Return SetError(1, 0, $adMsg)
	$dMID = DllStructGetData($tBuffer, 1, 2)
	If $dMID <> $MID_ROLAND Then Return SetError(1, 0, $adMsg)

	For $i = $iModelOffset + 1 To $iBinLen
		If DllStructGetData($tBuffer, 1, $i) Then ExitLoop
	Next
	If $i > $iBinLen Then Return SetError(1, 0, $adMsg)
	$iModelLen = $i - $iModelOffset

	$iCmdOffset = $iModelOffset + $iModelLen
	For $i = $iCmdOffset + 1 To $iBinLen
		If DllStructGetData($tBuffer, 1, $i) Then ExitLoop
	Next
	If $i > $iBinLen Then Return SetError(1, 0, $adMsg)
	$iCMDLen = $i - $iCmdOffset

	$iDataLen = $iBinLen - ($iCmdOffset + $iCMDLen + 1)
	$tagSysEx = StringFormat( _
			"byte SOX;byte MID;byte DEV[1];byte MODEL[%d];byte CMD[%d];byte DATA[%d];byte EOX", _
			$iModelLen, $iCMDLen, $iDataLen)
	If Not $iDataLen Then $tagSysEx = StringReplace($tagSysEx, "byte DATA[0];", "")

	$tSysEx = DllStructCreate($tagSysEx, DllStructGetPtr($tBuffer))

	$adMsg[0] = $dMID
	$adMsg[1] = DllStructGetData($tSysEx, "DEV")
	$adMsg[2] = DllStructGetData($tSysEx, "CMD")
	$adMsg[3] = DllStructGetData($tSysEx, "MODEL")
	$adMsg[4] = DllStructGetData($tSysEx, "DATA")
	If @error Then $adMsg[4] = Binary("0x")

	Return $adMsg
EndFunc   ;==>__midi_InspectSysEx_Roland

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_InspectSysEx_Uvsl
; Description ...: Deconstructs a universal system exclusive message header.
; Syntax ........: __midi_InspectSysEx_Uvsl($dSysEx)
; Parameters ....: $dSysEx - A system exclusive message
; Return values .: Success: A 5 element array of binary variants:
;                  $adMsg[0] - The manufacurer's ID, or a universal ID. (1 or 3 bytes)
;                  $adMsg[1] - The target device ID (1 byte)
;                  $adMsg[2] - The first Sub ID of the message (1 byte)
;                  $adMsg[3] - The second Sub ID of the message (1 byte)
;                  $adMsg[4] - Contains the data segment of the message for further deconstruction.
;                  Failure: An empty 5 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_InspectSysEx_Uvsl($dSysEx)
	Local $tBuffer, $tSysEx, $iBinLen = BinaryLen($dSysEx)
	Local $tagSysEx, $dMID, $iDataLen
	Local $adMsg[5]

	If $iBinLen < 6 Then Return SetError(1, 0, $adMsg)

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", BinaryLen($dSysEx)))
	DllStructSetData($tBuffer, 1, $dSysEx)
	If DllStructGetData($tBuffer, 1, 1) <> Binary("0xF0") Then Return SetError(1, 0, $adMsg)
	If DllStructGetData($tBuffer, 1, $iBinLen) <> Binary("0xF7") Then Return SetError(1, 0, $adMsg)
	$dMID = DllStructGetData($tBuffer, 1, 2)
	If $dMID <> $UID_NONREALTIME And $dMID <> $UID_REALTIME Then Return SetError(1, 0, $adMsg)

	$iDataLen = $iBinLen - 6
	$tagSysEx = StringFormat("byte SOX;byte MID[1];byte DEV[1];byte SUB1[1];byte SUB2[1];byte DATA[%d];byte EOX", $iDataLen)
	If Not ($iDataLen) Then $tagSysEx = StringReplace($tagSysEx, "byte DATA[0];", "")

	$tSysEx = DllStructCreate($tagSysEx, DllStructGetPtr($tBuffer))

	$adMsg[0] = $dMID
	$adMsg[1] = DllStructGetData($tSysEx, "DEV")
	$adMsg[2] = DllStructGetData($tSysEx, "SUB1")
	$adMsg[3] = DllStructGetData($tSysEx, "SUB2")
	$adMsg[4] = DllStructGetData($tSysEx, "DATA")
	If @error Then $adMsg[4] = Binary("0x")

	Return $adMsg
EndFunc   ;==>__midi_InspectSysEx_Uvsl

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_InspectSysEx_YamahaXG
; Description ...: Deconstructs a Yamaha XG system exclusive message header.
; Syntax ........: __midi_InspectSysEx_YamahaXG($dSysEx)
; Parameters ....: $dSysEx - A system exclusive message
; Return values .: Success: A 4 element array of binary variants:
;                  $adMsg[0] - The manufacurer's ID - $MID_YAMAHA (1 byte)
;                  $adMsg[1] - A device ID - (1 byte. Command: MS 4 bits, DevNum: LS 4 bits)
;                  $adMsg[2] - The model ID - $MODEL_YMHA_XG (1 byte)
;                  $adMsg[3] - Contains the data segment of the message for further deconstruction.
;                  Failure: An empty 4 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_InspectSysEx_YamahaXG($dSysEx)
	Local $tBuffer, $tSysEx, $iBinLen = BinaryLen($dSysEx)
	Local $tagSysEx, $dMID, $dModel, $iBodyLen

	Local $adMsg[4]

	If $iBinLen < 8 Then Return SetError(1, 0, $adMsg)

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", BinaryLen($dSysEx)))
	DllStructSetData($tBuffer, 1, $dSysEx)
	If DllStructGetData($tBuffer, 1, 1) <> Binary("0xF0") Then Return SetError(1, 0, $adMsg)
	If DllStructGetData($tBuffer, 1, $iBinLen) <> Binary("0xF7") Then Return SetError(1, 0, $adMsg)
	$dMID = DllStructGetData($tBuffer, 1, 2)
	If $dMID <> $MID_YAMAHA Then Return SetError(1, 0, $adMsg)
	$dModel = DllStructGetData($tBuffer, 1, 4)
	If $dModel <> $MODEL_YMHA_XG Then Return SetError(1, 0, $adMsg)

	$iBodyLen = $iBinLen - 5
	$tagSysEx = StringFormat( _
			"byte SOX;byte MID;byte DEVID[1];byte MODEL[1];byte DATA[%d];byte EOX", _
			$iBodyLen)
	If Not $iBodyLen Then $tagSysEx = StringReplace($tagSysEx, "byte DATA[0];", "")

	$tSysEx = DllStructCreate($tagSysEx, DllStructGetPtr($tBuffer))

	$adMsg[0] = $dMID
	$adMsg[1] = DllStructGetData($tSysEx, "DEVID")
	$adMsg[2] = DllStructGetData($tSysEx, "MODEL")
	$adMsg[3] = DllStructGetData($tSysEx, "DATA")
	If @error Then $adMsg[3] = Binary("0x")

	Return $adMsg
EndFunc   ;==>__midi_InspectSysEx_YamahaXG

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __midi_StartInput
; Description ...: Starts listening for midi messages on a midi input device.
; Syntax ........: __midi_StartInput($hDevice)
; Parameters ....: $hDevice - A midi input device handle.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: A device will stop automatically if a message is recieved and the input queue is full.
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __midi_StartInput($hDevice)
	Local $iIndex, $iBuffIdx

	_midiAPI_InStart($hDevice)
	If @error Then Return SetError(@error, @extended, False)

	For $i = 0 To UBound($__g_ahInputTimers) - 1
		If $__g_ahInputTimers[$i][0] = $hDevice Then ExitLoop
	Next
	$i = $iIndex

	If $iIndex = UBound($__g_ahInputTimers) Then ReDim $__g_ahInputTimers[$i + 1][2]
	$__g_ahInputTimers[$iIndex][0] = $hDevice
	$__g_ahInputTimers[$iIndex][1] = TimerInit()

	For $i = 1 To $__INPUTBUFFER_COUNT
		$iBuffIdx = __midi_AddBuffer($hDevice, $__INPUTBUFFER_SIZE)
		_midiAPI_InPrepareHeader($hDevice, $__g_avBuffers[$iBuffIdx][1])
		_midiAPI_InAddBuffer($hDevice, $__g_avBuffers[$iBuffIdx][1])
	Next

	Return True
EndFunc   ;==>__midi_StartInput

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __MM_MIM_DATA
; Description ...: Queues incoming midi messages for processing.
; Syntax ........: __MM_MIM_DATA($hWnd, $iMsg, $hDevice, $iData)
; Parameters ....: $hWnd - The window that recieved the message.
;                  $iMsg - $MM_MIM_DATA constant.
;                  $hDevice - A handle to the midi input device that recieved the message
;                  $iData - A short midi message encapsulated in a integer value.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __MM_MIM_DATA($hWnd, $iMsg, $hDevice, $iData)
	#forceref $hWnd, $iMsg, $hDevice, $iData
	Local $iTmrIdx, $iIndex

	For $i = 0 To UBound($__g_ahInputTimers) - 1
		If $__g_ahInputTimers[$i][0] = $hDevice Then ExitLoop
	Next
	$iTmrIdx = $i

	For $i = 0 To UBound($__g_adInputQueue) - 1
		If $__g_adInputQueue[$i][3] Then
			$__g_adInputQueue[$i][0] = 0
			$__g_adInputQueue[$i][1] = 0
			$__g_adInputQueue[$i][2] = 0
			$__g_adInputQueue[$i][3] = 0
		EndIf
	Next

	For $i = 0 To UBound($__g_adInputQueue) - 1
		If Not $__g_adInputQueue[$i][0] Then ExitLoop
	Next
	If $i = UBound($__g_adInputQueue) - 1 Then _midiAPI_InStop($hDevice)
	$iIndex = $i

	$__g_adInputQueue[$iIndex][0] = $hDevice
	$__g_adInputQueue[$iIndex][1] = Int($iData)
	$__g_adInputQueue[$iIndex][2] = TimerDiff($__g_ahInputTimers[$iTmrIdx][1])

;~ 	__midi_Debug($hDevice, $MIDI_DBG_IO_IN, $__g_adInputQueue[$iIndex][1])
EndFunc   ;==>__MM_MIM_DATA

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __MM_MIM_LONGDATA
; Description ...: Queues incoming midi messages for processing.
; Syntax ........: __MM_MIM_LONGDATA($hWnd, $iMsg, $hDevice, $pBuffHdr)
; Parameters ....: $hWnd - The window that recieved the message.
;                  $iMsg - $MM_MIM_LONGDATA constant.
;                  $hDevice - A handle to the midi input device that recieved the message
;                  $pBuffHdr - A pointer to a midi header structure, which contains the input buffer's location.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __MM_MIM_LONGDATA($hWnd, $iMsg, $hDevice, $pBuffHdr)
	#forceref $hWnd, $iMsg, $hDevice, $pBuffHdr

	Local $iTmrIdx, $iIndex
	Local $iBuffIdx, $tMidiHdr, $tBuffer, $dMsg
	$iBuffIdx = __midi_GetBufferIdx($pBuffHdr)
	$tMidiHdr = $__g_avBuffers[$iBuffIdx][2]
	$tBuffer = $__g_avBuffers[$iBuffIdx][3]

	$dMsg = BinaryMid(DllStructGetData($tBuffer, 1), 1, DllStructGetData($tMidiHdr, "dwBytesRecorded"))
	_midiAPI_InAddBuffer($hDevice, $pBuffHdr)

	For $i = 0 To UBound($__g_ahInputTimers) - 1
		If $__g_ahInputTimers[$i][0] = $hDevice Then ExitLoop
	Next
	$iTmrIdx = $i

	For $i = 0 To UBound($__g_adLongInputQueue) - 1
		If $__g_adLongInputQueue[$i][3] Then
			$__g_adLongInputQueue[$i][0] = 0
			$__g_adLongInputQueue[$i][1] = 0
			$__g_adLongInputQueue[$i][2] = 0
			$__g_adLongInputQueue[$i][3] = 0
		EndIf
	Next

	For $i = 0 To UBound($__g_adLongInputQueue) - 1
		If Not $__g_adLongInputQueue[$i][0] Then ExitLoop
	Next
	If $i = UBound($__g_adLongInputQueue) - 1 Then _midiAPI_InStop($hDevice)
	$iIndex = $i

	$__g_adLongInputQueue[$iIndex][0] = $hDevice
	$__g_adLongInputQueue[$iIndex][1] = $dMsg
	$__g_adLongInputQueue[$iIndex][2] = TimerDiff($__g_ahInputTimers[$iTmrIdx][1])

;~ 	__midi_Debug($hDevice, $MIDI_DBG_IO_IN, $dMsg)
EndFunc   ;==>__MM_MIM_LONGDATA

; #INTERNAL_USE_ONLY# ===========================================================================================================
; Name ..........: __MM_STATE_CHANGE
; Description ...: Tracks the open/closed states of midi devices
; Syntax ........: __MM_STATE_CHANGE($hWnd, $iMsg, $hDevice, $iReserved)
; Parameters ....: $hWnd - The window that recieved the message.
;                  $iMsg - $MM_*_OPEN or $MM_*_CLOSED message
;                  $hDevice - The handle to the midi device that has changed.
;                  $iReserved - 0.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func __MM_STATE_CHANGE($hWnd, $iMsg, $hDevice, $iReserved)
	#forceref $hWnd, $iMsg, $hDevice, $iReserved

	Local $iDevIdx

	For $i = 0 To UBound($__g_avDevStates) - 1
		If $__g_avDevStates[$i][0] = $hDevice Then ExitLoop
	Next
	$iDevIdx = $i

	If $iDevIdx = UBound($__g_avDevStates) Then
		ReDim $__g_avDevStates[$iDevIdx + 1][2]
		$__g_avDevStates[$iDevIdx][0] = $hDevice
	EndIf

	$__g_avDevStates[$iDevIdx][1] = $iMsg
EndFunc   ;==>__MM_STATE_CHANGE

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_CloseInput
; Description ...: Closes a midi input device.
; Syntax ........: _midi_CloseInput($hDevice)
; Parameters ....: $hDevice - A midi input device handle.
; Return values .: Success: True
;                  Failure: False, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_OpenInput
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_CloseInput($hDevice)
	Local Const $iTimeout = 500
	Local $hTimer, $iHdrFlags, $bResult

	GUIRegisterMsg($MM_MIM_LONGDATA, "")
	_midiAPI_InReset($hDevice)
	For $i = 0 To UBound($__g_avBuffers) - 1
		If $__g_avBuffers[$i][0] = $hDevice Then
			$hTimer = TimerInit()
			Do
				$iHdrFlags = DllStructGetData($__g_avBuffers[$i][2], "dwFlags")
				If BitAND($iHdrFlags, $MHDR_DONE) = $MHDR_DONE Then ExitLoop
				Sleep(10)
			Until TimerDiff($hTimer) > $iTimeout
			__midi_FreeBuffer($i)
		EndIf
	Next

	$bResult = _midiAPI_InClose($hDevice)
	If @error Then Return SetError(@error, @extended, $bResult)

	$hTimer = TimerInit()
	For $i = 0 To UBound($__g_avDevStates) - 1
		If ($__g_avDevStates[$i][0] = $hDevice) Then
			While TimerDiff($hTimer) < $__DEV_OPENCLOSE_TIMEOUT
				If $__g_avDevStates[$i][1] = $MM_MIM_CLOSE Then ExitLoop
				Sleep(10)
			WEnd
			ExitLoop
		EndIf
	Next

	GUIRegisterMsg($MM_MIM_LONGDATA, "__MM_MIM_LONGDATA")
	If TimerDiff($hTimer) > $__DEV_OPENCLOSE_TIMEOUT Then Return SetError(-1, 0, False)
	Return True
EndFunc   ;==>_midi_CloseInput

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_CloseOutput
; Description ...: Closes a midi output device.
; Syntax ........: _midi_CloseOutput($hDevice)
; Parameters ....: $hDevice - A midi output device handle.
; Return values .: Success: True
;                  Failure: False, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_CloseOutput($hDevice)
	Local Const $iTimeout = 500
	Local $hTimer, $iHdrFlags, $bResult

	_midiAPI_OutReset($hDevice)
	For $i = 0 To UBound($__g_avBuffers) - 1
		If $__g_avBuffers[$i][0] = $hDevice Then
			$hTimer = TimerInit()
			Do
				$iHdrFlags = DllStructGetData($__g_avBuffers[$i][2], "dwFlags")
				If BitAND($iHdrFlags, $MHDR_DONE) = $MHDR_DONE Then ExitLoop
				Sleep(10)
			Until TimerDiff($hTimer) > $iTimeout
			__midi_FreeBuffer($i)
		EndIf
	Next

	$bResult = _midiAPI_OutClose($hDevice)
	If @error Then Return SetError(@error, @extended, $bResult)

	$hTimer = TimerInit()
	For $i = 0 To UBound($__g_avDevStates) - 1
		If ($__g_avDevStates[$i][0] = $hDevice) Then
			While TimerDiff($hTimer) < $__DEV_OPENCLOSE_TIMEOUT
				If $__g_avDevStates[$i][1] = $MM_MOM_CLOSE Then ExitLoop
				Sleep(10)
			WEnd
			ExitLoop
		EndIf
	Next
	If TimerDiff($hTimer) > $__DEV_OPENCLOSE_TIMEOUT Then Return SetError(-1, 0, False)
	Return True
EndFunc   ;==>_midi_CloseOutput

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_DecrementNRPN
; Description ...: Decrements the value of a non-regestered parameter.
; Syntax ........: _midi_DecrementNRPN($hDevice, $iChannel, $iNRPN_MSB, $iNRPN_LSB)
; Parameters ....: $hDevice - Midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNRPN_MSB - Specifies the MSB of a NRPN.
;                  $iNRPN_LSB - Specifies the LSB of a NRPN.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: NRPNs are generally expressed as a MSB/LSB pair. If a 14 bit NRPN is specified by the manufacturer, $iNRPN_MSB
;                  represents the upper 7 bits of the NRPN while $iNRPN_LSB represents the lower 7 bits.
; Related .......: _midi_SetNRPN, _midi_IncrementNRPN
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_DecrementNRPN($hDevice, $iChannel, $iNRPN_MSB, $iNRPN_LSB)
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_NRPN_MSB, $iNRPN_MSB) Then Return False
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_NRPN_LSB, $iNRPN_LSB) Then Return False
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_DATA_DECR, 0) Then Return False
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_NRPN, $NRPN_NULL)
EndFunc   ;==>_midi_DecrementNRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_DecrementRPN
; Description ...: Decrements the value of a registered parameter.
; Syntax ........: _midi_DecrementRPN($hDevice, $iChannel, $iRPN)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iRPN - Specifies the RPN to decrement.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SetRPN, _midi_IncrementRPN
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_DecrementRPN($hDevice, $iChannel, $iRPN)
	If Not _midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $iRPN) Then Return False
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_DATA_DECR, 0) Then Return False
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $RPN_NULL)
EndFunc   ;==>_midi_DecrementRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_EnumInputs
; Description ...: Enumerates midi input devices.
; Syntax ........: _midi_EnumInputs()
; Parameters ....: None
; Return values .: Success: An array of device names, @extended is set to the number of devices found.
;                  Failure: An empty array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: The index of a device should be used with _midi_OpenInput.
; Related .......: _midi_OpenInput
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_EnumInputs()
	Local $tCaps, $pCaps, $asDevs[_midiAPI_InGetNumDevs()]
	If @error Then Return SetError(@error, @extended, $asDevs)

	$tCaps = DllStructCreate($tag_midiincaps)
	$pCaps = DllStructGetPtr($tCaps)

	For $i = 0 To UBound($asDevs) - 1
		_midiAPI_InGetDevCaps($i, $pCaps)
		If Not @error Then $asDevs[$i] = DllStructGetData($tCaps, "SzPname")
	Next
	If Not UBound($asDevs) Then Return SetError(-1, 0, $asDevs)
	Return SetExtended(UBound($asDevs), $asDevs)
EndFunc   ;==>_midi_EnumInputs

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_EnumOutputs
; Description ...: Enumerates midi output devices.
; Syntax ........: _midi_EnumOutputs()
; Parameters ....: None
; Return values .: Success: An array of device names, @extended is set to the number of devices found.
;                  Failure: An empty array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: The index of a device should be used with _midi_OpenOutput.
; Related .......: _midi_OpenOutput
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_EnumOutputs()
	Local $tCaps, $pCaps, $asDevs[_midiAPI_OutGetNumDevs()]
	If @error Then Return SetError(@error, @extended, $asDevs)

	$tCaps = DllStructCreate($tag_midioutcaps)
	$pCaps = DllStructGetPtr($tCaps)

	For $i = 0 To UBound($asDevs) - 1
		_midiAPI_OutGetDevCaps($i, $pCaps)
		If Not @error Then $asDevs[$i] = DllStructGetData($tCaps, "SzPname")
	Next
	If Not UBound($asDevs) Then Return SetError(-1, 0, $asDevs)

	Return SetExtended(UBound($asDevs), $asDevs)
EndFunc   ;==>_midi_EnumOutputs

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_GetInputName
; Description ...: Retrieves the name of an input device
; Syntax ........: _midi_GetInputName($hDevice)
; Parameters ....: $hDevice - A midi input device handle.
; Return values .: Success: The device name
;                  Failure: An empty string, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_GetInputName($hDevice)
	Local $tCaps = DllStructCreate($tag_midiincaps)
	If Not IsPtr($hDevice) Then Return SetError(-1, 0, "")
	_midiAPI_InGetDevCaps($hDevice, DllStructGetPtr($tCaps))
	If @error Then Return SetError(@error, 0, False)
	Return DllStructGetData($tCaps, "szPName")
EndFunc   ;==>_midi_GetInputName

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_GetOutputName
; Description ...: Retrieves the name of an output device
; Syntax ........: _midi_GetOutputName($hDevice)
; Parameters ....: $hDevice - A midi output device handle.
; Return values .: Success: The device name
;                  Failure: An empty string, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_GetOutputName($hDevice)
	Local $tCaps = DllStructCreate($tag_midioutcaps)
	If Not IsPtr($hDevice) Then Return SetError(-1, 0, "")
	_midiAPI_OutGetDevCaps($hDevice, DllStructGetPtr($tCaps))
	If @error Then Return SetError(@error, 0, False)
	Return DllStructGetData($tCaps, "szPName")
EndFunc   ;==>_midi_GetOutputName

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_IncrementNRPN
; Description ...: Increments the value of a non-registered parameter.
; Syntax ........: _midi_IncrementNRPN($hDevice, $iChannel, $iNRPN_MSB, $iNRPN_LSB)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNRPN_MSB - Specifies the MSB of a NRPN.
;                  $iNRPN_LSB - Specifies the LSB of a NRPN.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: NRPNs are generally expressed as a MSB/LSB pair. If a 14 bit NRPN is specified by the manufacturer, $iNRPN_MSB
;                  represents the upper 7 bits of the NRPN while $iNRPN_LSB represents the lower 7 bits.
; Related .......: _midi_SetNRPN, _midi_DecrementNRPN
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_IncrementNRPN($hDevice, $iChannel, $iNRPN_MSB, $iNRPN_LSB)
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_NRPN_MSB, $iNRPN_MSB) Then Return False
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_NRPN_LSB, $iNRPN_LSB) Then Return False
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_DATA_INCR, 0) Then Return False
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_NRPN, $NRPN_NULL)
	Return True
EndFunc   ;==>_midi_IncrementNRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_IncrementRPN
; Description ...: Increments the value of a registered parameter.
; Syntax ........: _midi_IncrementRPN($hDevice, $iChannel, $iRPN)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iRPN - Specifies which RPN to increment.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SetRPN, _midi_DecrementRPN
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_IncrementRPN($hDevice, $iChannel, $iRPN)
	If Not _midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $iRPN) Then Return False
	If Not _midi_SendControlChange($hDevice, $iChannel, $CC_DATA_INCR, 0) Then Return False
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $RPN_NULL)
	Return True
EndFunc   ;==>_midi_IncrementRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_OpenInput
; Description ...: Opens a midi input device for operations.
; Syntax ........: _midi_OpenInput($iDeviceID, $hWnd)
; Parameters ....: $iDeviceID - A Device ID.
;                  $hWnd - Specifies a window to recieve incoming midi messages.
; Return values .: Success: A midi input device handle
;                  Failure: False, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: A device ID can be obtained via _midi_EnumInputs.
; Related .......: _midi_EnumInputs, _midi_CloseInput
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_OpenInput($iDeviceID, $hWnd)
	Local $hDevice, $hTimer
	If Not (IsHWnd($hWnd) And WinExists($hWnd)) Then Return False

	$hDevice = _midiAPI_InOpen($iDeviceID, $hWnd, 0, $CALLBACK_WINDOW)
	If @error Then Return SetError(@error, @extended, False)

	$hTimer = TimerInit()
	While TimerDiff($hTimer) < $__DEV_OPENCLOSE_TIMEOUT
		For $i = 0 To UBound($__g_avDevStates) - 1
			If ($__g_avDevStates[$i][0] = $hDevice) And _
					($__g_avDevStates[$i][1] = $MM_MIM_OPEN) Then ExitLoop 2
		Next
		Sleep(10)
	WEnd
	If TimerDiff($hTimer) > $__DEV_OPENCLOSE_TIMEOUT Then Return SetError(-1, 0, False)

	__midi_StartInput($hDevice)
	If @error Then Return SetError(@error, @extended, False)

	Return $hDevice
EndFunc   ;==>_midi_OpenInput

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_OpenOutput
; Description ...: Opens a midi device for operations.
; Syntax ........: _midi_OpenOutput($iDeviceID, $hWnd)
; Parameters ....: $iDeviceID - A Device ID.
;                  $hWnd - Specifies a window to recieve incoming midi messages.
; Return values .: Success: A midi output device handle
;                  Failure: False, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: A device ID can be obtained via _midi_EnumOutputs.
; Related .......: _midi_EnumOutputs, _midi_CloseOutput
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_OpenOutput($iDeviceID, $hWnd)
	Local $hDevice, $hTimer
	If Not (IsHWnd($hWnd) And WinExists($hWnd)) Then Return False

	$hDevice = _midiAPI_OutOpen($iDeviceID, $hWnd, 0, $CALLBACK_WINDOW)
	If @error Then Return SetError(@error, @extended, False)

	$hTimer = TimerInit()
	While TimerDiff($hTimer) < $__DEV_OPENCLOSE_TIMEOUT
		For $i = 0 To UBound($__g_avDevStates) - 1
			If ($__g_avDevStates[$i][0] = $hDevice) And _
					($__g_avDevStates[$i][1] = $MM_MOM_OPEN) Then ExitLoop 2
		Next
		Sleep(10)
	WEnd
	If TimerDiff($hTimer) > $__DEV_OPENCLOSE_TIMEOUT Then Return SetError(-1, 0, False)

	Return $hDevice
EndFunc   ;==>_midi_OpenOutput

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_PackAddress
; Description ...: Converts an integer representation of an address into a binary variant
; Syntax ........: _midi_PackAddress($iAddress[, $iLength = 3])
; Parameters ....: $iAddress - An integer representation of an address.
;                  $iLength - The target length of the binary.
; Return values .: A binary variant
; Author ........: MattyD
; Modified ......:
; Remarks .......: For use with SysEx functions that require an address parameter.
; Related .......: _midiUnpackAddress
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_PackAddress($iAddress, $iLength = 3)
	Local $tBin = DllStructCreate(StringFormat("byte[%d]", $iLength))
	For $i = 0 To $iLength - 1
		DllStructSetData($tBin, 1, BitAND(0x7F, $iAddress), $iLength - $i)
		$iAddress = BitShift($iAddress, 8)
	Next
	Return DllStructGetData($tBin, 1)
EndFunc   ;==>_midi_PackAddress

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_PackSize
; Description ...: Converts a number into a binary variant
; Syntax ........: _midi_PackSize($iSize[, $iLength = 3])
; Parameters ....: $iSize - An integer.
;                  $iLength - The target length of the binary.
; Return values .: A binary variant
; Author ........: MattyD
; Modified ......:
; Remarks .......: For use with SysEx functions that require a size parameter.
; Related .......: _midi_UnpackSize
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_PackSize($iSize, $iLength = 3)
	Local $tBin = DllStructCreate(StringFormat("byte[%d]", $iLength))
	For $i = 0 To $iLength - 1
		DllStructSetData($tBin, 1, BitAND(0x7F, $iSize), $iLength - $i)
		$iSize = BitShift($iSize, 7)
	Next
	Return DllStructGetData($tBin, 1)
EndFunc   ;==>_midi_PackSize

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_Read14bitCtrlChange
; Description ...: Returns the value of a 14bit controller.
; Syntax ........: _midi_Read14bitCtrlChange($hDevice, $aiMsg, $iMSBCtrl)
; Parameters ....: $hDevice - A midi input device handle.
;                  $aiMsg - An array returned from _midi_ReadMsg
;                  $iMSBCtrl - The MSB of the control to read.
; Return values .: Success: The the value of the control.
;                  Failure: -1, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function keeps track of CC mesages. A 14bit value is returned when the LSB control is read.
;                  $CC_EXPRESSION, $CC_NRPN and $CC_RPN are internally reset if a "Reset All Controllers" message is recieved.
;                  Valid $iMSBCtrl values are 0 - 0x1F, $CC_NRPN and $CC_RPN.
; Related .......: _midi_ReadMsg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_Read14bitCtrlChange($hDevice, $aiMsg, $iMSBCtrl)
	Local Static $aiCCRegister[0][17][128]
	Local $iIndex, $iLSBCtrl, $iMSB, $aiResetRng[2] = [0, -1]

	If UBound($aiMsg) <> 4 Or $aiMsg[0] <> $MSG_CC Then Return SetError(1, 0, -1)

	For $i = 0 To UBound($aiCCRegister) - 1
		If $aiCCRegister[$i][0][0] = $hDevice Then ExitLoop
	Next
	$iIndex = $i

	If $iMSBCtrl = $MM_RESET_CTRLS Then
		$aiResetRng[0] = $aiMsg[1]
		$aiResetRng[1] = $aiMsg[1]
	EndIf

	If $iIndex = UBound($aiCCRegister) Then
		ReDim $aiCCRegister[$iIndex + 1][17][128]
		$aiCCRegister[$iIndex][0][0] = $hDevice
		$aiResetRng[0] = 1
		$aiResetRng[1] = 16
	EndIf

	For $i = $aiResetRng[0] To $aiResetRng[1]
		$aiCCRegister[$iIndex][$i][$CC_EXPRESSION] = 0x7F
		$aiCCRegister[$iIndex][$i][$CC_EXPRESSION_LSB] = 0x7F
		$aiCCRegister[$iIndex][$i][$CC_NRPN_LSB] = 0x7F
		$aiCCRegister[$iIndex][$i][$CC_NRPN_MSB] = 0x7F
		$aiCCRegister[$iIndex][$i][$CC_RPN] = 0x7F
		$aiCCRegister[$iIndex][$i][$CC_RPN_LSB] = 0x7F
		$aiCCRegister[$iIndex][$i][$CC_RPN_MSB] = 0x7F
	Next

	Switch $iMSBCtrl
		Case $CC_NRPN, $CC_RPN
			$iLSBCtrl = $iMSBCtrl - 1
		Case 0 To 0x1F
			$iLSBCtrl = $iMSBCtrl + 0x20
		Case Else
			Return SetError(1, 0, -1)
	EndSwitch

	$aiCCRegister[$iIndex][$aiMsg[1]][$aiMsg[2]] = $aiMsg[3]
	If $aiMsg[2] = $iMSBCtrl Then
		$aiCCRegister[$iIndex][$aiMsg[1]][$iLSBCtrl] = 0
		Return SetError(1, 0, -1)
	EndIf

	$iMSB = $aiCCRegister[$iIndex][$aiMsg[1]][$iMSBCtrl]

	Return BitShift($iMSB, -7) + $aiMsg[3]
EndFunc   ;==>_midi_Read14bitCtrlChange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadIDReply
; Description ...: Deconstructs an ID Reply system exclusive message.
; Syntax ........: _midi_ReadIDReply($dSysEx)
; Parameters ....: $dSysEx - A system exclusive message.
; Return values .: Success: A 5 element array of binary variants:
;                  $adID[0] = Manfacture's SysEx ID (1 or 3 bytes)
;                  $adID[1] = Device ID (1 byte)
;                  $adID[2] = Device Family (2 bytes)
;                  $adID[3] = Model (2 bytes)
;                  $adID[4] = Software Version (device specific, typically 4 bytes)
;                  Failure: an empty 5 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:_midi_ReadSysEx
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadIDReply($dSysEx)
	Local $adID[5], $adMsg, $tagData, $tData, $tBuffer, $iDataLen
	Local $iMIDLen = 1, $iSWVerLen

	$adMsg = __midi_InspectSysEx_Uvsl($dSysEx)
	If @error Then Return SetError(1, 0, $adID)

	If $adMsg[0] <> $UID_NONREALTIME Then Return SetError(1, 0, $adID)
	If $adMsg[2] <> $NRTID_INFO Then Return SetError(1, 0, $adID)
	If $adMsg[3] <> Binary("0x02") Then Return SetError(1, 0, $adID)
	If Not BinaryMid($adMsg[4], 1, 1) Then $iMIDLen = 3

	$iDataLen = BinaryLen($adMsg[4])
	$iSWVerLen = $iDataLen - ($iMIDLen + 4)

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iDataLen))
	DllStructSetData($tBuffer, 1, $adMsg[4])
	$tagData = StringFormat("byte MID[%d];byte FAM[2];byte MODEL[2];byte VER[%d]", $iMIDLen, $iSWVerLen)
	If Not $iSWVerLen Then $tagData = StringReplace($tagData, ";byte VER[0]", "")
	$tData = DllStructCreate($tagData, DllStructGetPtr($tBuffer))

	$adID[0] = DllStructGetData($tData, "MID")
	$adID[1] = $adMsg[1]
	$adID[2] = DllStructGetData($tData, "FAM")
	$adID[3] = DllStructGetData($tData, "MODEL")
	$adID[4] = DllStructGetData($tData, "VER")

	Return $adID
EndFunc   ;==>_midi_ReadIDReply

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadMsg
; Description ...: Retrieves and deconstructs a short message from the input queue.
; Syntax ........: _midi_ReadMsg($hDevice)
; Parameters ....: $hDevice - A midi input device handle.
; Return values .: Succes: A 4 element array of integers.
;                  $aiMsg[0] = The message type ($MSG_* Value)
;                  $aiMsg[1] = The target midi channel, or basic channel (1 - 16)
;                  $aiMsg[2] = The first parameter of the message.
;                  $aiMsg[3] = The second parameter of the message if required.
;                  Failure: An empty 4 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: Messages will be discarded from the input queue if the are not retrieved within 2 seconds of arrival.
;                  The parameter elements are implemented as below:
;                  Message type   | Param 1                 | Param 2
;                  $MSG_NOTE_OFF  | Note (0 - 127)          | Velocity (0 - 127)
;                  $MSG_NOTE_ON   | Note (0 - 127)          | Velocity (0 - 127)
;                  $MSG_CHAN_PRES | Pressure (0 - 127)      | -
;                  $MSG_CC        | Controller ID (0 - 127) | Value (0 - 127)
;                  $MSG_PC        | Program (1 - 128)       | -
;                  $MSG_KEY_PRES  | Key (0 - 127)           | Pressure (0 - 127)
;                  $MSG_BEND      | Bend Value (0 - 0x3FFF) | -
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadMsg($hDevice)
	Local $iMsg, $aiMsg[4], $iIndex
	Local Static $tMsg = DllStructCreate("byte[3]")

	$iIndex = __midi_GetMsg($hDevice)
	If $iIndex < 0 Then Return SetError(1, $aiMsg)

	$iMsg = $__g_adInputQueue[$iIndex][1]

	DllStructSetData($tMsg, 1, $iMsg)
	$aiMsg[0] = BitAND(0xF0, DllStructGetData($tMsg, 1, 1))
	$aiMsg[1] = BitAND(0x0F, DllStructGetData($tMsg, 1, 1)) + 1
	$aiMsg[2] = DllStructGetData($tMsg, 1, 2)
	$aiMsg[3] = DllStructGetData($tMsg, 1, 3)

	Switch $aiMsg[0]
		Case $MSG_PC
			$aiMsg[2] += 1
		Case $MSG_BEND
			$aiMsg[2] = BitShift($aiMsg[3], -7) + $aiMsg[2]
			$aiMsg[3] = 0
	EndSwitch

	$__g_adInputQueue[$iIndex][3] = True

	Return $aiMsg
EndFunc   ;==>_midi_ReadMsg

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadPatchChange
; Description ...: Returns the bank and program values on a "Program Change" message.
; Syntax ........: _midi_ReadPatchChange($hDevice, $aiMsg)
; Parameters ....: $hDevice - A midi output device handle.
;                  $aiMsg - An array returned from _midi_ReadMsg
; Return values .: Success: A 3 element array of integers
;                  $aiProgram[0] The 1 based program number
;                  $aiProgram[1] The MSB of the selected bank
;                  $aiProgram[2] The LSB of the selected bank
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function tracks 14bit $CC_BANK control change messages.
;                  The stored bank and program values are returned on a $MSG_PC message.
; Related .......: _midi_ReadMsg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadPatchChange($hDevice, $aiMsg)
	Local Static $aiBankRegister[0][17]
	Local $iIndex, $iBank, $aiProgram[3]

	If UBound($aiMsg) <> 4 Then Return SetError(1, 0, $aiProgram)

	For $i = 0 To UBound($aiBankRegister) - 1
		If $aiBankRegister[$i][0] = $hDevice Then ExitLoop
	Next
	$iIndex = $i
	If $iIndex = UBound($aiBankRegister) Then
		ReDim $aiBankRegister[$iIndex + 1][17]
		$aiBankRegister[$iIndex][0] = $hDevice
	EndIf

	$iBank = _midi_Read14bitCtrlChange($hDevice, $CC_BANK, $aiMsg)
	If $iBank > -1 Then $aiBankRegister[$iIndex][$aiMsg[1]] = $iBank

	If $aiMsg[0] <> $MSG_PC Then Return SetError(1, 0, $aiProgram)
	$iBank = $aiBankRegister[$iIndex][$aiMsg[1]]

	$aiProgram[0] = $aiMsg[2]
	$aiProgram[1] = BitShift($iBank, 7)
	$aiProgram[2] = BitAND($iBank, 0x7F)
	Return $aiProgram

EndFunc   ;==>_midi_ReadPatchChange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadRolandDT1
; Description ...: Decontsructs a Roland DT1 SysEx message.
; Syntax ........: _midi_ReadRolandDT1($dSysEx, $iAddressLen)
; Parameters ....: $dSysEx - a system exclusive message.
;                  $iAddressLen - The length of the address segment of the message.
; Return values .: Success: A 3 element array of binary variants:
;                  $adDT1[0] = Model of the device.
;                  $adDT1[1] = Address
;                  $adDT1[2] = Data
;                  Failure: an empty 3 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function will fail if the checksum of the message is invalid.
; Related .......: _midi_ReadSysEx
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadRolandDT1($dSysEx, $iAddressLen)
	Local $adDT1[3], $adMsg, $tagBody, $tBody, $iBodyLen, $tBuffer
	Local $iSum, $iDataLen

	$adMsg = __midi_InspectSysEx_Roland($dSysEx)
	If @error Then Return SetError(1, 0, $adDT1)

	If $adMsg[2] <> $RLDID_DT1 Then Return SetError(1, 0, $adDT1)
	$iBodyLen = BinaryLen($adMsg[4])

	If $iAddressLen < 0 Or $iAddressLen > ($iBodyLen - 1) Then Return SetError(1, 0, $adDT1)

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iBodyLen))
	DllStructSetData($tBuffer, 1, $adMsg[4])

	For $i = 1 To $iBodyLen
		$iSum += Int(DllStructGetData($tBuffer, 1, $i))
	Next
	If BitAND($iSum, 0x7F) Then Return SetError(1, 0, $adDT1)

	$iDataLen = $iBodyLen - ($iAddressLen + 1)
	$tagBody = StringFormat("byte ADD[%d];byte DATA[%d];byte SUM", $iAddressLen, $iDataLen)
	If Not $iDataLen Then $tagBody = StringReplace($tagBody, "byte DATA[0];", "")
	$tBody = DllStructCreate($tagBody, DllStructGetPtr($tBuffer))

	$adDT1[0] = $adMsg[3]
	$adDT1[1] = DllStructGetData($tBody, "ADD")
	$adDT1[2] = DllStructGetData($tBody, "DATA")
	If Not $iDataLen Then $adDT1[2] = Binary("0x")

	Return $adDT1
EndFunc   ;==>_midi_ReadRolandDT1

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadRPN
; Description ...: Retrieves an RPN value on recieving a data, increment or decrement CC message.
; Syntax ........: _midi_ReadRPN($hDevice, $aiMsg)
; Parameters ....: $hDevice - A midi input device handle.
;                  $aiMsg - An array returned from _midi_ReadMsg
; Return values .: Success: a 3 element array:
;                  $aiRPNData[0] = The RPN that has been updated
;                  $aiRPNData[1] = The MSB, or 14bit value of the RPN
;                  $aiRPNData[2] = The LSB value of the RPN (if required)
;                  Failure: an empty 3 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function tracks RPN values based on "Control Change" messages.
;                  If a "Reset Controllers" message is recieved, subsequent data change messages will apply to $RPN_NULL and
;                  thus be ignored until a new RPN is specified.
;                  The channel specified by "Reset Controllers" is treated as an literal channel for this function.
;                  Data control change messages are handled as follows:
;                  RPN                  | CC Length   | +/- | Default   | Comment
;                  $RPN_BEND_SENS       | 14 or 7 bit | LSB | 0x02/0x00 | LSB(cents) wraps to MSB(semitones) @100 where possible.
;                  $RPN_FINE_TUNING     | 14bit       | LSB | 0x2000    | MSB and LSB is a combined value.
;                  $RPN_COURSE_TUNING   | 7 bit       | MSB | 0x40      | LSB is ignored
;                  $RPN_TUNING_PGM_SEL  | 7 bit Only  | MSB | 0x00      | LSB is ignored
;                  $RPN_TUNING_BANK_SEL | 7 bit Only  | MSB | 0x00      | LSB is ignored
;                  $RPN_MOD_DEPTH_RANGE | 14 or 7 bit | LSB | 0x00/0x40 | MSB = semitones. LSB = 100/128 cents, LSB wraps @128.
; Related .......: _midi_SendRolandRQ1, _midi_ReadMsg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadRPN($hDevice, $aiMsg)
	Local Const $RPN_DUMMY = 6, $RPN_CURRENT = 7
	Local Static $aiRPNRegister[0][17][8]
	Local $iDev, $iRPN = -1, $iData = -1, $aiRPNData[3]
	Local $iIncrement, $iMSB, $iLSB

	If UBound($aiMsg) <> 4 Then Return SetError(1, 0, $aiRPNData)

	For $i = 0 To UBound($aiRPNRegister) - 1
		If $aiRPNRegister[$i][0][0] = $hDevice Then ExitLoop
	Next
	$iDev = $i
	If $iDev = UBound($aiRPNRegister) Then
		ReDim $aiRPNRegister[$iDev + 1][17][8]
		$aiRPNRegister[$iDev][0][0] = $hDevice
		For $j = 1 To 16
			$aiRPNRegister[$iDev][$j][$RPN_BEND_SENS] = 0x100
			$aiRPNRegister[$iDev][$j][$RPN_FINE_TUNING] = 0x2000
			$aiRPNRegister[$iDev][$j][$RPN_COURSE_TUNING] = 0x2000
			$aiRPNRegister[$iDev][$j][$RPN_MOD_DEPTH_RANGE] = 0x40
			$aiRPNRegister[$iDev][$j][$RPN_CURRENT] = $RPN_DUMMY
		Next
	EndIf

	If $aiMsg[0] <> $MSG_CC Then Return SetError(1, 0, $aiRPNData)

	$iRPN = _midi_Read14bitCtrlChange($hDevice, $CC_RPN, $aiMsg)
	If $iRPN = $RPN_NULL Then $iRPN = $RPN_DUMMY
	If $iRPN > -1 And $iRPN < $RPN_DUMMY Then $aiRPNRegister[$iDev][$aiMsg[1]][$RPN_CURRENT] = $iRPN
	$iRPN = $aiRPNRegister[$iDev][$aiMsg[1]][$RPN_CURRENT]

	Switch $iRPN
		Case $RPN_BEND_SENS, $RPN_MOD_DEPTH_RANGE
			If $aiMsg[2] = $CC_DATA Then $iData = BitShift($aiMsg[3], -7)
			If $aiMsg[2] = $CC_DATA_LSB Then
				$iData = $aiRPNRegister[$iDev][$j][$iRPN]
				$iData = BitAND($iRPN, 0x3F80) + $aiMsg[3]
			EndIf
		Case $RPN_FINE_TUNING
			$iData = _midi_Read14bitCtrlChange($hDevice, $CC_DATA, $aiMsg)
		Case $RPN_COURSE_TUNING, $RPN_TUNING_PGM_SEL, $RPN_TUNING_BANK_SEL
			If $aiMsg[2] = $CC_DATA Then $iData = BitShift($aiMsg[3], -7)
	EndSwitch
	If $iData > -1 Then $aiRPNRegister[$iDev][$aiMsg[1]][$iRPN] = $iData

	If $aiMsg[2] = $CC_DATA_INCR Then $iIncrement = 1
	If $aiMsg[2] = $CC_DATA_DECR Then $iIncrement = -1
	If $iIncrement Then
		$iData = $aiRPNRegister[$iDev][$aiMsg[1]][$iRPN]
		Switch $iRPN
			Case $RPN_COURSE_TUNING, $RPN_TUNING_PGM_SEL, $RPN_TUNING_BANK_SEL
				$iData += (0x80 * $iIncrement)
				If $iData > 0x3F80 Then $iData = 0x3F80
				If $iData < 0 Then $iData = 0

			Case $RPN_BEND_SENS
				$iMSB = BitShift($iData, 7)
				$iLSB = BitAND($iData, 0x7F)

				$iLSB += $iIncrement
				If $iMSB < 0x7F Then
					$iMSB += Floor($iLSB / 100)
					$iLSB = Mod($iLSB, 100)
				EndIf
				$iData = BitShift($iMSB, -7) + $iLSB
				If $iData > 0x3FFF Then $iData = 0x3FFF
				If $iData < 0 Then $iData = 0

			Case Else
				$iData += $iIncrement
				If $iData > 0x3FFF Then $iData = 0x3FFF
				If $iData < 0 Then $iData = 0
		EndSwitch
	EndIf

	If $iData = -1 Or $iRPN = $RPN_DUMMY Then Return SetError(1, 0, $aiRPNData)

	$aiRPNRegister[$iDev][$aiMsg[1]][$iRPN] = $iData
	$aiRPNData[0] = $iRPN
	$iMSB = BitShift($iData, 7)
	$iLSB = BitAND($iData, 0x7F)
	Switch $iRPN
		Case $RPN_BEND_SENS, $RPN_MOD_DEPTH_RANGE
			$aiRPNData[1] = $iMSB
			$aiRPNData[2] = $iLSB
		Case $RPN_COURSE_TUNING, $RPN_TUNING_PGM_SEL, $RPN_TUNING_BANK_SEL
			$aiRPNData[1] = $iMSB
		Case $RPN_FINE_TUNING
			$aiRPNData[1] = $iData
	EndSwitch

	Return $aiRPNData
EndFunc   ;==>_midi_ReadRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadSysEx
; Description ...: Retrieves a system exclusive message from the input queue.
; Syntax ........: _midi_ReadSysEx($hDevice, Byref $dSysEx)
; Parameters ....: $hDevice - a midi input device handle.
;                  $dSysEx - [out] a system exclusive message.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadSysEx($hDevice, ByRef $dSysEx)
	Local Const $dNULL = Binary("0x")
	Local Static $aData[0][2]
	Local $tBuffer, $tBuffer2
	Local $iOldBinLen, $iAddBinLen

	If Not __midi_GetLongMsg($hDevice, $dSysEx) Then Return False

	For $i = 0 To UBound($aData) - 1
		If $aData[$i][0] = $hDevice Then ExitLoop
	Next
	If $i = UBound($aData) Then
		ReDim $aData[$i + 1][2]
		$aData[$i][0] = $hDevice
		$aData[$i][1] = $dNULL
	EndIf

	If BinaryMid($dSysEx, 1, 1) <> Binary("0xF0") Then
		$iOldBinLen = BinaryLen($aData[$i][1])
		If $iOldBinLen Then
			$iAddBinLen = BinaryLen($dSysEx)
			$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iOldBinLen + $iAddBinLen))
			$tBuffer2 = DllStructCreate(StringFormat("byte[%d];byte[%d]", $iOldBinLen, $iAddBinLen), DllStructGetPtr($tBuffer))
			DllStructSetData($tBuffer2, 1, $aData[$i][1])
			DllStructSetData($tBuffer2, 2, $dSysEx)
			$dSysEx = DllStructGetData($tBuffer, 1)
		EndIf
	EndIf

	If BinaryMid($dSysEx, 1, 1) <> Binary("0xF0") Then $dSysEx = $dNULL
	For $j = 2 To BinaryLen($dSysEx) - 1
		If Int(DllStructGetData($tBuffer, 1, $j)) >= 0x80 Then
			$dSysEx = $dNULL
			ExitLoop
		EndIf
	Next

	If BinaryMid($dSysEx, BinaryLen($dSysEx), 1) = Binary("0xF7") Then
		$aData[$i][1] = $dNULL
		Return True
	Else
		$aData[$i][1] = $dSysEx
		$dSysEx = $dNULL
		Return False
	EndIf

EndFunc   ;==>_midi_ReadSysEx

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadXGDataDump
; Description ...: Deconstructs a Yamaha XG Data Dump SysEx message.
; Syntax ........: _midi_ReadXGDataDump($dSysEx)
; Parameters ....: $dSysEx - a system exclusive message.
; Return values .: Success: A 3 element array of binary variants:
;                  $adDT1[0] = Zero based Yamaha device number. (0 - 0x0F)
;                  $adDT1[1] = Address
;                  $adDT1[2] = Data
;                  Failure: an empty 3 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function will fail if the checksum of the message is invalid.
;                  This function returns the device number in its raw form. Increment by 1 for the ID as displayed on the
;                  physical device.
; Related .......: _midi_SendXGDumpRequest, _midi_ReadSysEx
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadXGDataDump($dSysEx)
	Local $adData[3], $adMsg, $tagBody, $tBody, $iBodyLen, $tBuffer
	Local $dDevNumber, $iSum, $iDataLen

	$adMsg = __midi_InspectSysEx_YamahaXG($dSysEx)
	If @error Then Return SetError(1, 0, $adData)

	If BitAND(0xF0, $adMsg[1]) <> $YAMXG_DATA_DUMP Then Return SetError(1, 0, $adData)
	$dDevNumber = Binary(Chr(BitAND(0x0F, $adMsg[1])))
	$iBodyLen = BinaryLen($adMsg[4])

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iBodyLen))
	DllStructSetData($tBuffer, 1, $adMsg[4])

	For $i = 3 To $iBodyLen
		$iSum += Int(DllStructGetData($tBuffer, 1, $i))
	Next
	If BitAND($iSum, 0x7F) Then Return SetError(1, 0, $adData)

	$iDataLen = $iBodyLen - 6
	$tagBody = StringFormat("byte SIZE[2];byte ADD[3];byte DATA[%d];byte SUM", $iDataLen)
	If Not $iDataLen Then $tagBody = StringReplace($tagBody, "byte DATA[0];", "")

	$adData[0] = $dDevNumber
	$adData[1] = DllStructGetData($tBody, "ADD")
	$adData[2] = DllStructGetData($tBody, "DATA")
	If Not $iDataLen Then $adData[2] = Binary("0x")

	Return $adData
EndFunc   ;==>_midi_ReadXGDataDump

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_ReadXGParamChg
; Description ...: Deconstructs a Yamaha XG Parameter Change SysEx message.
; Syntax ........: _midi_ReadXGParamChg($dSysEx)
; Parameters ....: $dSysEx - a system exclusive message.
; Return values .: Success: A 3 element array of binary variants:
;                  $adDT1[0] = Zero based Yamaha device number. (0 - 0x0F)
;                  $adDT1[1] = Address
;                  $adDT1[2] = Data
;                  Failure: an empty 3 element array, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function returns the device number in its raw form. Increment by 1 for the ID as displayed on the
;                  physical device.
; Related .......: _midi_SendXGParamRequest, _midi_ReadSysEx
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_ReadXGParamChg($dSysEx)
	Local $adData[3], $adMsg, $tagBody, $tBody, $iBodyLen, $tBuffer
	Local $dDevNumber, $iDataLen

	$adMsg = __midi_InspectSysEx_YamahaXG($dSysEx)
	If @error Then Return SetError(1, 0, $adData)

	If BitAND(0xF0, $adMsg[1]) <> $YAMXG_PARAM_CHG Then Return SetError(1, 0, $adData)
	$dDevNumber = Binary(Chr(BitAND(0x0F, $adMsg[1])))
	$iBodyLen = BinaryLen($adMsg[4])

	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iBodyLen))
	DllStructSetData($tBuffer, 1, $adMsg[4])

	$iDataLen = $iBodyLen - 3
	$tagBody = StringFormat("byte ADD[3];byte DATA[%d]", $iDataLen)
	If Not $iDataLen Then $tagBody = StringReplace($tagBody, "byte DATA[0];", "")

	$adData[0] = $dDevNumber
	$adData[1] = DllStructGetData($tBody, "ADD")
	$adData[2] = DllStructGetData($tBody, "DATA")
	If Not $iDataLen Then $adData[2] = Binary("0x")

	Return $adData
EndFunc   ;==>_midi_ReadXGParamChg

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_RegDebugFunc
; Description ...: Registers a user defined function to be called when midi messages are recieved or sent.
; Syntax ........: _midi_RegDebugFunc($sFunction)
; Parameters ....: $sFunction - The function to be called.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......: The function to be registered must have 3 paramters as indicated below:
;                  Param1 (handle) - Recieves a midi device handle.
;                  Param2 (int)    - Recieves $MIDI_DBG_IO_IN or $MIDI_DBG_IO_OUT.
;                  Param3 (binary) - Recieves a binary midi message.
;                  If is possible for Param3 to contain a partial sysex message if it spans multiple buffers.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
;~ Func _midi_RegDebugFunc($sFunction)
;~ 	$__g_sDebugFunc = $sFunction
;~ EndFunc   ;==>_midi_RegDebugFunc

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SelectPatch
; Description ...: Loads a patch from a specified bank on a midi channel.
; Syntax ........: _midi_SelectPatch($hDevice, $iChannel, $iProgram[, $iBankCourse = 0[, $iBankFine = 0]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iProgram - Specifies which patch number to select. (1 - 128)
;                  $iBankCourse - The MSB of a bank. (0 - 127)
;                  $iBankFine - The LSB of a bank (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Modern instruments usually represent a bank using MSB ("Course") and LSB ("Fine") Values.
;                  If a single 14 bit value is required, it may be more appropriate to select a patch by the following sequence:
;                  - call _midi_Send14bitCtrlChange with controller 0.
;                  - call _midi_SendProgramChange.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SelectPatch($hDevice, $iChannel, $iProgram, $iBankCourse = 0, $iBankFine = 0)
	Local $bResult
	_midi_SendControlChange($hDevice, $iChannel, $CC_BANK, $iBankCourse)
	If @error Then Return SetError(@error, @extended, False)
	_midi_SendControlChange($hDevice, $iChannel, $CC_BANK_LSB, $iBankFine)
	If @error Then Return SetError(@error, @extended, False)
	$bResult = _midi_SendProgramChange($hDevice, $iChannel, $iProgram)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SelectPatch

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_Send14bitCtrlChange
; Description ...: Sends "Control Change" messages where a 14-bit resolution is required.
; Syntax ........: _midi_Send14bitCtrlChange($hDevice, $iChannel, $iMSBCtrl, $iValue)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iMSBCtrl - Specifies the most significant control. (0 to 31, 99, 101)
;                  $iValue - Specifies a value between 0 and 0x3FFF
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function sends two "Control Change" messages. The upper 7 bits of $iValue are sent to the MSB control
;                  specified by $iMSBCtrl, then the lower 7 bits are sent to an associated LSB control.
;                  The LSB control is determined as follows:
;                  If $iMSBCtrl = 0 to 31, then the LSB controller number = $iMSBCtrl + 0x20
;                  If $iMSBCtrl = 99, then LSB ctrl# = 98
;                  If $iMSBCtrl = 101, then LSB ctrl# = 100
; Related .......: _midi_SendControlChange
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_Send14bitCtrlChange($hDevice, $iChannel, $iMSBCtrl, $iValue)
	Local $iLSBCtrl, $bResult

	If $iValue < 0 Or $iValue > 0x3FFF Then Return False
	Switch $iMSBCtrl
		Case $CC_NRPN, $CC_RPN
			$iLSBCtrl = $iMSBCtrl - 1
		Case 0 To 0x1F
			$iLSBCtrl = $iMSBCtrl + 0x20
		Case Else
			Return False
	EndSwitch
	_midi_SendControlChange($hDevice, $iChannel, $iMSBCtrl, BitAND(0x7F, BitShift($iValue, 7)))
	If @error Then Return SetError(@error, @extended, False)
	$bResult = _midi_SendControlChange($hDevice, $iChannel, $iLSBCtrl, BitAND(0x7F, $iValue))
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_Send14bitCtrlChange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendAllNotesOff
; Description ...: Sends a "All Notes Off" message to a midi device.
; Syntax ........: _midi_SendAllNotesOff($hDevice[, $iChannel = 1])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - A midi channel.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function is not intended to be used as a replacement for "Note Off" messages.
;                  Like "Note Off" messages, "All Notes Off" should only affect those triggered via an instrument's midi input -
;                  locally played notes should not be affected.
;                  The response to this message can vary depending on the manufacturer, vintage and operation mode of the
;                  instrument (omni/mono/poly etc). Modern devices will generally turn off notes for the channel specified in the
;                  message. Alternately, a device may require the message to be sent on its nominated "basic" channel, and will
;                  respond by switching off notes across all channels.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendAllNotesOff($hDevice, $iChannel)
	Local $bResult = _midi_SendControlChange($hDevice, $iChannel, 0X7B, 0)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendAllNotesOff

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendAllSoundOff
; Description ...: Sends a "All Sound Off" message to a midi device.
; Syntax ........: _midi_SendAllSoundOff($hDevice[, $iChannel = 1])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - A midi channel.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The response to this message can vary depending on the manufacturer and vintage of the device.
;                  Modern devices genrally respond by stopping sounds on the channel specified in the message.
;                  Some instruments may require the message to be sent on its nominated "basic" channel, and respond by turning
;                  off sounds on all channels.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendAllSoundOff($hDevice, $iChannel)
	Local $bResult = _midi_SendControlChange($hDevice, $iChannel, 0X78, 0)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendAllSoundOff

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendChannelPressure
; Description ...: Sends a "Channel Pressure" message to a specified channel on a midi device.
; Syntax ........: _midi_SendChannelPressure($hDevice, $iChannel, $iPressure)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iPressure - A pressure value. (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SendPolyKeyPressure
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendChannelPressure($hDevice, $iChannel, $iPressure)
	Local $iMsg, $bResult = False
	$iMsg = __midi_BuildShortMsg($MSG_CHAN_PRES, $iChannel, $iPressure, 0)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendChannelPressure

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendControlChange
; Description ...: Sends a "Control Change" message to a specified channel on a midi device.
; Syntax ........: _midi_SendControlChange($hDevice, $iChannel, $iControl, $iValue)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iControl - Specifies which control to update
;                  $iValue - Specifies the controller's value
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_Send14bitCtrlChange
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendControlChange($hDevice, $iChannel, $iControl, $iValue)
	Local $iMsg, $bResult = False
	$iMsg = __midi_BuildShortMsg($MSG_CC, $iChannel, $iControl, $iValue)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendControlChange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendIDRequest
; Description ...: Sends a Device ID Request.
; Syntax ........: _midi_SendIDRequest($hDevice[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - a midi output device handle.
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_ReadSysEx, _midi_ReadIDReply
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendIDRequest($hDevice, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer
	__midi_BuildSysEx_Uvsl($tBuffer, $UID_NONREALTIME, $dDeviceID, $NRTID_INFO, 1)
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SendIDRequest

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendMsg
; Description ...: Sends a short midi message
; Syntax ........: _midi_SendMsg($hDevice, $aiMsg)
; Parameters ....: $hDevice - A midi output device handle
;                  $aiMsg - An 4 element array containing a deconstructed midi message.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: See _midi_ReadMsg for the implementation of $aiMsg.
; Related .......: _midi_ReadMsg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendMsg($hDevice, $aiMsg)
	Local $iMsg, $bResult
	If UBound($aiMsg) <> 4 Then Return False
	$iMsg = __midi_BuildShortMsg($aiMsg[0], $aiMsg[1], $aiMsg[2], $aiMsg[3])
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendMsg

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendNoteOff
; Description ...: Sends a "Note Off" message to a specified channel on a midi device.
; Syntax ........: _midi_SendNoteOff($hDevice, $iChannel, $iNote[[, $iVelocity = 0], $bHiRes = False])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected. (1 - 16)
;                  $iNote - A note value. (0 - 127)
;                  $iVelocity - A velocity value. (0 - 127), (0 - 0x3FFF High Resolution)
;                  $bHiRes - Indicates $iVelocity is a 14bit value if true.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: "Note Off" messages do not have any effect on notes triggered by physically playing an instrument.
;                  $iVelocity values <= 0x7F are treated as 0 when using high resolution velocities.
; Related .......: _midi_SendNoteOn
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendNoteOff($hDevice, $iChannel, $iNote, $iVelocity = 0, $bHiRes = False)
	Local $iMsg, $bResult
	If $bHiRes Then
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_VELOCITY_LSB, BitAND($iVelocity, 0x7F))
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
		$iVelocity = BitShift($iVelocity, 7)
	EndIf
	$iMsg = __midi_BuildShortMsg($MSG_NOTE_OFF, $iChannel, $iNote, $iVelocity)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendNoteOff

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendNoteOn
; Description ...: Sends a "Note On" message to a specified channel on a midi device.
; Syntax ........: _midi_SendNoteOn($hDevice, $iChannel, $iNote, $iVelocity[, $bHiRes = False])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected. (1 - 16)
;                  $iNote - A note value. (0 - 127)
;                  $iVelocity - A velocity value. (0 - 127), (0 - 0x3FFF High Resolution)
;                  $bHiRes - Indicates $iVelocity is a 14bit value if true.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: A "Note On" message with a velocity of 0 is equivalant to a "Note Off" message.
;                  When using high resolution velocities, any $iVelocity <= 0x7F is equivalant to a "Note Off" message.
; Related .......: _midi_SendNoteOff
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendNoteOn($hDevice, $iChannel, $iNote, $iVelocity, $bHiRes = False)
	Local $iMsg, $bResult
	If $bHiRes Then
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_VELOCITY_LSB, BitAND($iVelocity, 0x7F))
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
		$iVelocity = BitShift($iVelocity, 7)
	EndIf
	$iMsg = __midi_BuildShortMsg($MSG_NOTE_ON, $iChannel, $iNote, $iVelocity)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendNoteOn

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendPitchBend
; Description ...: Sends a "Pitch Bend" message to a specified channel on a midi device.
; Syntax ........: _midi_SendPitchBend($hDevice, $iChannel[, $iBend = 0x2000])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected. (1 - 16)
;                  $iBend - A pitch bend value (0 - 0x2000 - 0x3FFF)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid pitch bend values range from 0 to 0x3FFF, with 0x2000 signifying no bend.
; Related .......: _midi_SetPitchBendSens
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendPitchBend($hDevice, $iChannel, $iBend = 0x2000)
	Local $iMsg, $bResult
	$iMsg = __midi_BuildShortMsg($MSG_BEND, $iChannel, $iBend)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendPitchBend

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendPolyKeyPressure
; Description ...: Sends a "Polyphonic Key Pressure" message to a specified channel on a midi device.
; Syntax ........: _midi_SendPolyKeyPressure($hDevice, $iChannel, $iKey, $iPressure)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected. (1 - 16)
;                  $iKey - A note value. (0 - 127)
;                  $iPressure - A pressure value. (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SendChannelPressure
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendPolyKeyPressure($hDevice, $iChannel, $iKey, $iPressure)
	Local $iMsg, $bResult
	$iMsg = __midi_BuildShortMsg($MSG_KEY_PRES, $iChannel, $iKey, $iPressure)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendPolyKeyPressure

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendProgramChange
; Description ...: Sends a "Program Change" message to a specified channel on a midi device.
; Syntax ........: _midi_SendProgramChange($hDevice, $iChannel, $iProgram)
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected. (0 - 127)
;                  $iProgram - Specifies which program number to select. (1 - 128)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The program parameter is 1 based, with a maximum value of 128.
;                  This message should generally be preceded by selecting a bank. Use _midi_SelectPatch to achieve this.
; Related .......: _midi_SelectPatch
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendProgramChange($hDevice, $iChannel, $iProgram)
	Local $iMsg, $bResult
	$iMsg = __midi_BuildShortMsg($MSG_PC, $iChannel, $iProgram, 0)
	If $iMsg Then $bResult = _midiAPI_OutShortMsg($hDevice, $iMsg)
;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $iMsg)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendProgramChange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendResetControllers
; Description ...: Sends a "Reset All Controllers" message to a midi device.
; Syntax ........: _midi_SendResetControllers($hDevice[, $iChannel = 1])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - A midi channel. (1 - 16)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Modern devices should generally reset controllers on the channel specified in the message.
;                  The following is recommended practice upon recieving a "reset all controllers" message:
;                  $CC_MODWHEEL     - 0
;                  $CC_EXPRESSION   - 0x7F
;                  $CC_DAMPER       - 0
;                  $CC_PORTAMENTO   - 0
;                  $CC_SOSTENUTO    - 0
;                  $CC_SOFTPEDAL    - 0
;                  $CC_RPN          - $RPN_NULL
;                  $CC_NRPN         - $NRPN_NULL
;                  Channel pressure - 0
;                  Key Pressures    - 0
;                  Pitch Bend       - 0x2000
;                  $CC_VOLUME       - Unmodified
;                  $CC_PAN          - Unmodified
;                  $CC_PORT_TIME    - Unmodified
;                  $CC_REVERB       - Unmodified
;                  $CC_CHORUS       - Unmodified
;                  Other supported  - 0, unless documented by the manufacturer
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendResetControllers($hDevice, $iChannel)
	Local $bResult = _midi_SendControlChange($hDevice, $iChannel, 0X79, 0)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendResetControllers

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendRolandDT1
; Description ...: Sends a Roland DT1 SysEx message to a midi device.
; Syntax ........: _midi_SendRolandDT1($hDevice, $dModel, $dAddress, $dData[, $dDeviceID = $DEVID_ROLAND_DEF])
; Parameters ....: $hDevice - A midi output device handle.
;                  $dModel - Specifies the model of the device.
;                  $dAddress - Specifies the address to write to.
;                  $dData - Specifies the data to write.
;                  $dDeviceID - Specifies the device Id to target. Default is 0x10
; Author ........: MattyD
; Modified ......:
; Remarks .......: The length of the binary parameters must match what is specified in the device's documentation.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendRolandDT1($hDevice, $dModel, $dAddress, $dData, $dDeviceID = $DEVID_ROLAND_DEF)
	Local $dMsg, $tBuffer, $tBody, $pBody, $tagBody
	Local $iBodyLen, $iBodyOffset, $iSum
	Local $iAddressLen = BinaryLen($dAddress), $iDataLen = BinaryLen($dData)

	$iBodyLen = $iAddressLen + $iDataLen
	$pBody = __midi_BuildSysEx_Roland($tBuffer, $dDeviceID, $dModel, Binary("0x12"), $iBodyLen + 1)
	$tagBody = StringFormat("byte[%d];byte[%d];byte", $iAddressLen, $iDataLen)
	$tBody = DllStructCreate($tagBody, $pBody)
	DllStructSetData($tBody, 1, $dAddress)
	DllStructSetData($tBody, 2, $dData)

	$iBodyOffset = Number($pBody) - Number(DllStructGetPtr($tBuffer))

	For $i = $iBodyOffset + 1 To $iBodyOffset + $iBodyLen
		$iSum += DllStructGetData($tBuffer, 1, $i)
	Next
	$iSum = BitAND(BitNOT($iSum) + 1, 0x7F)
	DllStructSetData($tBody, 3, $iSum)

	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SendRolandDT1

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendRolandGSReset
; Description ...: Requests a device to change its mode of operation to best support the Roland GS standard.
; Syntax ........: _midi_SendRolandGSReset($hDevice[, $dDeviceID = $DEVID_ROLAND_DEF])
; Parameters ....: $hDevice - A midi output device handle.
;                  $dDeviceID - Specifies the device Id to target. Default is 0x10
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SetGMMode
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendRolandGSReset($hDevice, $dDeviceID = $DEVID_ROLAND_DEF)
	Local Const $dAddress = Binary("0x40007F"), $dData = Binary("0x00")
	Local $bResult = _midi_SendRolandDT1($hDevice, $MODEL_RLND_GS, $dAddress, $dData, $dDeviceID)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendRolandGSReset

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendRolandRQ1
; Description ...: Sends a Roland RQ1 SysEx message
; Syntax ........: _midi_SendRolandRQ1($hDevice, $dModel, $dAddress, $dData[, $dDeviceID = $DEVID_ROLAND_DEF])
; Parameters ....: $hDevice - a midi output device handle.
;                  $dModel - the model of the device.
;                  $dAddress - the address to write to.
;                  $dSize - the amount of data to recieve in bytes.
;                  $dDeviceID - the device Id to target. Default is 0x10
; Author ........: MattyD
; Modified ......:
; Remarks .......: The length of the binary parameters must match what is specified in the device's documentation.
; Related .......: _midi_ReadRolandDT1
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendRolandRQ1($hDevice, $dModel, $dAddress, $dSize, $dDeviceID = $DEVID_ROLAND_DEF)
	Local $dMsg, $tBuffer, $tBody, $pBody, $tagBody
	Local $iBodyLen, $iBodyOffset, $iSum
	Local $iAddressLen = BinaryLen($dAddress), $iSizeLen = BinaryLen($dSize)

	$iBodyLen = $iAddressLen + $iSizeLen
	$pBody = __midi_BuildSysEx_Roland($tBuffer, $dDeviceID, $dModel, Binary("0x11"), $iBodyLen + 1)
	$tagBody = StringFormat("byte[%d];byte[%d];byte", $iAddressLen, $iSizeLen)
	$tBody = DllStructCreate($tagBody, $pBody)
	DllStructSetData($tBody, 1, $dAddress)
	DllStructSetData($tBody, 2, $dSize)

	$iBodyOffset = Number($pBody) - Number(DllStructGetPtr($tBuffer))

	For $i = $iBodyOffset + 1 To $iBodyOffset + $iBodyLen
		$iSum += DllStructGetData($tBuffer, 1, $i)
	Next
	$iSum = BitAND(BitNOT($iSum) + 1, 0x7F)
	DllStructSetData($tBody, 3, $iSum)

	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SendRolandRQ1

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendSysEx
; Description ...: Sends a system exclusive message.
; Syntax ........: _midi_SendSysEx($hDevice, $dSysEx)
; Parameters ....: $hDevice - A midi output device handle.
;                  $dSysEx - A system exclusive binary message
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_ReadSysEx
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendSysEx($hDevice, $dSysEx)

	Local Const $iTimeout = 500
	Local $iBuffIdx, $pMidiHdr
	Local $hTimer, $iHdrFlags, $bResult

	If Not IsBinary($dSysEx) Then Return False

	$iBuffIdx = __midi_AddBuffer($hDevice, BinaryLen($dSysEx))
	$pMidiHdr = $__g_avBuffers[$iBuffIdx][1]
	__midi_FillBuffer($iBuffIdx, $dSysEx)

	_midiAPI_OutPrepareHeader($hDevice, $pMidiHdr)
	_midiAPI_OutLongMsg($hDevice, $pMidiHdr)

	$hTimer = TimerInit()
	Do
		$iHdrFlags = DllStructGetData($__g_avBuffers[$iBuffIdx][2], "dwFlags")
		If BitAND($iHdrFlags, $MHDR_DONE) = $MHDR_DONE Then ExitLoop
		Sleep(10)
	Until TimerDiff($hTimer) > $iTimeout
	$bResult = (TimerDiff($hTimer) <= $iTimeout)

	_midiAPI_OutUnprepareHeader($hDevice, $pMidiHdr)
	__midi_FreeBuffer($iBuffIdx)

;~ 	If $bResult Then __midi_Debug($hDevice, $MIDI_DBG_IO_OUT, $dSysEx)

	Return $bResult
EndFunc   ;==>_midi_SendSysEx

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendXGDumpRequest
; Description ...: Sends a Yamaha XD data dump request SysEx message
; Syntax ........: _midi_SendXGDumpRequest($hDevice, $dAddress[, $iDeviceNum = 1])
; Parameters ....: $hDevice - a midi output device handle.
;                  $dAddress - the 3-byte address to read from.
;                  $iDeviceNum - the 1 based Yamaha device number. (1 - 16)
; Author ........: MattyD
; Modified ......:
; Remarks .......: The address parameter must be binary value with a length of exactly 3 bytes.
; Related .......: _midi_SendXGParamRequest, _midi_ReadXGDataDump
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendXGDumpRequest($hDevice, $dAddress, $iDeviceNum = 1)
	Local $dMsg, $tBuffer
	__midi_BuildSysEx_YamahaXG($tBuffer, $iDeviceNum - 1, $YAMXG_DUMP_REQ, $dAddress)
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SendXGDumpRequest

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendXGParamChange
; Description ...: Sends a Yamaha XD parameter change SysEx message
; Syntax ........: _midi_SendXGParamChange($hDevice, $dAddress, $dData[, $iDeviceNum = 1])
; Parameters ....: $hDevice - a midi output device handle.
;                  $dAddress - the 3-byte address to start writing from.
;                  $dData - the binary data to write.
;                  $iDeviceNum - the 1 based Yamaha device number. (1 - 16)
; Author ........: MattyD
; Modified ......:
; Remarks .......: The length of the binary parameters must match what is specified in the device's documentation.
; Related .......: _midi_ReadXGParamChg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendXGParamChange($hDevice, $dAddress, $dData, $iDeviceNum = 1)
	Local $dMsg, $tBuffer, $tData, $pData
	Local $iDataLen = BinaryLen($dData)

	$pData = __midi_BuildSysEx_YamahaXG($tBuffer, $iDeviceNum - 1, $YAMXG_PARAM_CHG, $dAddress, $iDataLen)
	$tData = DllStructCreate(StringFormat("byte[%d]", $iDataLen), $pData)
	DllStructSetData($tData, 1, $dData)

	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SendXGParamChange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendXGParamRequest
; Description ...: Sends a Yamaha XD paramater request SysEx message
; Syntax ........: _midi_SendXGParamRequest($hDevice, $dAddress[, $iDeviceNum = 1])
; Parameters ....: $hDevice - a midi output device handle.
;                  $dAddress - the 3-byte address to read from.
;                  $iDeviceNum - the 1 based Yamaha device number. (1 - 16)
; Author ........: MattyD
; Modified ......:
; Remarks .......: The address parameter must be binary value with a length of exactly 3 bytes.
; Related .......: _midi_SendXGDumpRequest, _midi_ReadXGParamChg
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendXGParamRequest($hDevice, $dAddress, $iDeviceNum = 1)
	Local $dMsg, $tBuffer
	__midi_BuildSysEx_YamahaXG($tBuffer, $iDeviceNum - 1, $YAMXG_PARAM_REQ, $dAddress)
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SendXGParamRequest

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SendXGSystemOn
; Description ...: Requests a device to change its mode of operation to best support the Yamaha XG standard.
; Syntax ........: _midi_SendXGSystemOn($hDevice[, $iDeviceNum = 0])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iDeviceNum - A Yamaha device number (1 - 16)
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SetGMMode
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SendXGSystemOn($hDevice, $iDeviceNum = 1)
	Local Const $dAddress = Binary("0x00007E"), $dData = Binary("0x00")
	Local $bResult = _midi_SendXGParamChange($hDevice, $dAddress, $dData, $iDeviceNum)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SendXGSystemOn

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetADSR_Attack
; Description ...: Changes the release time on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetADSR_Attack($hDevice, $iChannel, $iTime = 0x40)
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iTime - The relative change to the attack time (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iTime values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetADSR_Decay, _midi_SetADSR_Release
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetADSR_Attack($hDevice, $iChannel, $iTime = 0x40)
	Local $bResult
	If $iTime < 0 Or $iTime > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_ENVELOPE_MSB, $NRPN_ENV_ATTACK, $iTime)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetADSR_Attack

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetADSR_Decay
; Description ...: Changes the release time on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetADSR_Decay($hDevice, $iChannel, $iTime = 0x40)
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iTime - The relative change to the decay time. (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iTime values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetADSR_Attack, _midi_SetADSR_Release
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetADSR_Decay($hDevice, $iChannel, $iTime = 0x40)
	Local $bResult
	If $iTime < 0 Or $iTime > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_ENVELOPE_MSB, $NRPN_ENV_DECAY, $iTime)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetADSR_Decay

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetADSR_Release
; Description ...: Changes the release time on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetADSR_Release($hDevice, $iChannel, $iTime = 0x40)
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iTime - The relative change to the release time (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iTime values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetADSR_Attack, _midi_SetADSR_Decay
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetADSR_Release($hDevice, $iChannel, $iTime = 0x40)
	Local $bResult
	If $iTime < 0 Or $iTime > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_ENVELOPE_MSB, $NRPN_ENV_RELEASE, $iTime)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetADSR_Release

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetAftertouchDest
; Description ...: Sets parameters around controls that aftertouch can manipulate.
; Syntax ........: _midi_SetAftertouchDest($hDevice, $iSource, $iChannel[, $aiDests = Default, [, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iSource - The type of aftertouch. ($CSRC_CHAN_PRES, $CSRC_KEY_PRES)
;                  $iChannel - The 1 based midi channel to target.
;                  $aiDests - Specifies what parameters to control, and thier ranges.
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $aiDests is 2 dimention array defined as:
;                  $aiDests[index][0] = Parameter
;                  $aiDests[index][1] = Range
;                  Valid parameters and ranges are defined as below.
;                  Parameter                 | Range       | Meaning                  | Default
;                  $CDEST_PITCH              | 0x28 - 0x58 | -24 To +24 semitones     | 0x40
;                  $CDEST_FILTER_CUTOFF      | 0 - 0x7F    | -9600 To +9450 cents     | 0x40
;                  $CDEST_AMPLITUDE          | 0 - 0x7F    | 0 - 100*(127/64) percent | 0x40
;                  $CDEST_LFO_PITCH          | 0 - 0x7F    | 0 - 600 cents            | 0
;                  $CDEST_LFO_FILTER         | 0 - 0x7F    | 0 - 2400 cents           | 0
;                  $CDEST_LFO_AMPLITUDE      | 0 - 0x7F    | 0 - 100 percent          | 0
; Related .......: _midi_SetControllerDest
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetAftertouchDest($hDevice, $iSource, $iChannel, $aiDests = Default, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tBody, $pBody, $iParamCnt
	Local $aiDefProps[6][2] = [[$CDEST_PITCH, 0x40], [$CDEST_FILTER_CUTOFF, 0x40], [$CDEST_AMPLITUDE, 0x40], _
			[$CDEST_LFO_PITCH, 0], [$CDEST_LFO_FILTER, 0], [$CDEST_LFO_AMPLITUDE, 0]]

	If $aiDests = Default Then $aiDests = $aiDefProps
	$iParamCnt = UBound($aiDests)
	If $iParamCnt > 6 Then Return False
	If $iSource < 0 Or $iSource > 1 Then Return False
	If $iChannel < 1 Or $iChannel > 16 Then Return False

	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_CTRL_DEST, $iSource, 2 * $iParamCnt + 1)
	$tBody = DllStructCreate(StringFormat("byte;byte[%d]", 2 * $iParamCnt), $pBody)
	DllStructSetData($tBody, 1, $iChannel - 1)
	For $i = 0 To $iParamCnt - 1
		DllStructSetData($tBody, 2, $aiDests[$i][0], 2 * $i + 1)
		DllStructSetData($tBody, 2, $aiDests[$i][1], 2 * $i + 2)
	Next

	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False

	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetAftertouchDest

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChanCourseTuning
; Description ...: Sets the tuning for a specified midi channel.
; Syntax ........: _midi_SetChanCourseTuning($hDevice, $iChannel[, $iTuning = 0x40])
; Parameters ....: $hDevice - a midi output device.
;                  $iChannel - The midi channel to retune.
;                  $iTuning - The displacment from A440 in 100 cent increments.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $iTuning is implemented as follows.
;                  minimum: 0          = -6400 cents (-64 semitones)
;                  neutral: 64  (0x40) = No displacement from A440
;                  maximum: 127 (0x7F) = +6300 cents (+63 semitones)
; Related .......: _midi_SetChanFineTuning
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChanCourseTuning($hDevice, $iChannel, $iTuning = 0x40)
	If $iTuning < 0 Or $iTuning > 0x7F Then Return False
	Local $bResult = _midi_SetRPN($hDevice, $iChannel, $RPN_COURSE_TUNING, $iTuning)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetChanCourseTuning

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChanFineTuning
; Description ...: Sets the tuning for a specified midi channel.
; Syntax ........: _midi_SetChanFineTuning($hDevice, $iChannel[, $iTuning = 0x2000])
; Parameters ....: $hDevice - a midi output device.
;                  $iChannel - The midi channel to retune. (1 - 16)
;                  $iTuning - The displacment from A440 in 100/8192 cent increments. (0 - 0x2000 - 0x3FFF)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $iTuning is implemented as follows.
;                  minimum: 0      = -100 cents (-1 semitone)
;                  neutral: 0x2000 = No displacement from A440
;                  maximum: 0x3FFF = + 8191 * 100/8192 cents (~+1 semitone)
; Related .......: _midi_SetChanCourseTuning
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChanFineTuning($hDevice, $iChannel, $iTuning = 0x2000)
	If $iTuning < 0 Or $iTuning > 0x3FFF Then Return False
	Local $bResult = _midi_SetRPN($hDevice, $iChannel, $RPN_FINE_TUNING, $iTuning, True)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetChanFineTuning

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChorusDepth
; Description ...: Sets the chorus depth on General Midi 2 compliant devices.
; Syntax ........: _midi_SetChorusDepth($hDevice, $iDepth[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iDepth - Specifies the the peak to peak swing of the modulation in time. (0 - 127)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The the peak to peak swing of the modulation can be calculated as:
;                  Swing in milliseconds = ($iDepth + 1) / 3.2
; Related .......: _midi_SetChorusType, _midi_SetChorusRate, _midi_SetChorusFeedback, _midi_SetChorusReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChorusDepth($hDevice, $iDepth, $dDeviceID = $DEVID_BROADCAST)
	If $iDepth < 0 Or $iDepth > 0x7F Then Return False
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_CHS, $CHSP_MOD_DEPTH, Binary(Chr($iDepth)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetChorusDepth

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChorusFeedback
; Description ...: Sets the amount of chorus feedback on General Midi 2 compliant devices.
; Syntax ........: _midi_SetChorusFeedback($hDevice, $iFeedback[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iFeedback - Specifies the amount of feedback from the Chorus output. (0 - 127)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The amount of feedback can be calculated as:
;                  Feedback as a percentage = $iFeedback * 0.763
; Related .......: _midi_SetChorusType, _midi_SetChorusDepth, _midi_SetChorusRate, _midi_SetChorusReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChorusFeedback($hDevice, $iFeedback, $dDeviceID = $DEVID_BROADCAST)
	If $iFeedback < 0 Or $iFeedback > 0x7F Then Return False
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_CHS, $CHSP_FEEDBACK, Binary(Chr($iFeedback)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetChorusFeedback

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChorusRate
; Description ...: Sets the chorus modulation rate on General Midi 2 compliant devices.
; Syntax ........: _midi_SetChorusRate($hDevice, $iRate[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iRate - Specifies the modulation frequency (0 - 127)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The modulation frequency can be calculated as:
;                  Frquency in Hz = $iRate * 0.122
; Related .......: _midi_SetChorusType, _midi_SetChorusDepth, _midi_SetChorusFeedback, _midi_SetChorusReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChorusRate($hDevice, $iRate, $dDeviceID = $DEVID_BROADCAST)
	If $iRate < 0 Or $iRate > 0x7F Then Return False
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_CHS, $CHSP_MOD_RATE, Binary(Chr($iRate)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetChorusRate

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChorusReverb
; Description ...: Sets the reverb send level for the chorus unit on General Midi 2 compliant devices.
; Syntax ........: _midi_SetChorusReverb($hDevice, $iLevel[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iLevel - Specifies the reverb send level of the chorus unit (0 - 127)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The reverb send level can be calculated as:
;                  Level a percentage = $iLevel * 0.787
; Related .......: _midi_SetChorusType, _midi_SetChorusDepth, _midi_SetChorusRate, _midi_SetChorusFeedback
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChorusReverb($hDevice, $iLevel, $dDeviceID = $DEVID_BROADCAST)
	If $iLevel < 0 Or $iLevel > 0x7F Then Return False
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_CHS, $CHSP_RVB_SEND, Binary(Chr($iLevel)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetChorusReverb

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetChorusType
; Description ...: Sets parameters of the chorus unit on General Midi 2 compliant devices.
; Syntax ........: _midi_SetChorusType($hDevice[, $iType = $CHS_CHORUS3[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iType - Specifies the chorus type.
;                  $dDeviceID - [optional] a binary variant value. Default is $DEVID_BROADCAST.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid chrous types and thier default properties are listed below:
;                  Preset        | Feedback  | Mod Rate  | Mod Depth  | Rev Send
;                  $CHS_CHORUS1  | 0   (0%)  | 3 (0.4Hz) | 5  (1.9ms) | 0 (0%)
;                  $CHS_CHORUS2  | 5   (4%)  | 9 (1.1Hz) | 19 (6.3ms) | 0 (0%)
;                  $CHS_CHORUS3  | 8   (6%)  | 3 (0.4Hz) | 19 (6.3ms) | 0 (0%)
;                  $CHS_CHORUS4  | 16  (12%) | 9 (1.1Hz) | 16 (5.3ms) | 0 (0%)
;                  $CHS_FBCHORUS | 64  (49%) | 2 (0.2Hz) | 24 (7.8ms) | 0 (0%)
;                  $CHS_FLANGER  | 112 (86%) | 1 (0.1Hz) | 5  (1.9ms) | 0 (0%)
; Related .......: _midi_SetChorusDepth, _midi_SetChorusRate, _midi_SetChorusFeedback, _midi_SetChorusReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetChorusType($hDevice, $iType = $CHS_CHORUS3, $dDeviceID = $DEVID_BROADCAST)
	If $iType < 0 Or $iType > 5 Then Return False
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_CHS, $CHSP_TYPE, Binary(Chr($iType)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetChorusType

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetControllerDest
; Description ...: Sets parameters around controls that some continuos controllers can manipulate.
; Syntax ........: _midi_SetControlDest($hDevice, $iChannel, $iControl[, $aiDests = Default[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - The 1 based midi channel to target.
;                  $iControl - Specifies the cotroller to assign
;                  $aiDests - Specifies what parameters to control, and thier ranges.
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False, @error <> 0
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iControl values are: 0x01 To 0x1F, and 0x40 To 0x5F
;                  $aiDests is 2 dimention array defined as $aiDests[Parameter][Range]
;                  Valid parameters and ranges are defined as below.
;                  Parameter                 | Range       | Meaning                  | Default
;                  $CDEST_PITCH              | 0x28 - 0x58 | -24 To +24 semitones     | 0x40
;                  $CDEST_FILTER_CUTOFF      | 0 - 0x7F    | -9600 To +9450 cents     | 0x40
;                  $CDEST_AMPLITUDE          | 0 - 0x7F    | 0 - 100*(127/64) percent | 0x40
;                  $CDEST_LFO_PITCH          | 0 - 0x7F    | 0 - 600 cents            | 0
;                  $CDEST_LFO_FILTER         | 0 - 0x7F    | 0 - 2400 cents           | 0
;                  $CDEST_LFO_AMPLITUDE      | 0 - 0x7F    | 0 - 100 percent          | 0
;                  Previous destinations and ranges for a channel will be cleared when calling this function.
; Related .......: _midi_SetAftertouchDest
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetControllerDest($hDevice, $iChannel, $iControl, $aiDests = Default, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tData, $pData, $iParamCnt
	Local $aiDefProps[6][2] = [[$CDEST_PITCH, 0x40], [$CDEST_FILTER_CUTOFF, 0x40], [$CDEST_AMPLITUDE, 0x40], _
			[$CDEST_LFO_PITCH, 0], [$CDEST_LFO_FILTER, 0], [$CDEST_LFO_AMPLITUDE, 0]]

	If $aiDests = Default Then $aiDests = $aiDefProps
	$iParamCnt = UBound($aiDests)
	If $iParamCnt > 6 Then Return False
	If $iControl < 1 Or $iControl > 0x5F Then Return False
	If $iControl > 0x1F And $iControl < 0x40 Then Return False
	If $iChannel < 1 Or $iChannel > 16 Then Return False

	$pData = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_CTRL_DEST, 3, 2 * $iParamCnt + 2)
	$tData = DllStructCreate(StringFormat("byte;byte;byte[%d]", 2 * $iParamCnt), $pData)
	DllStructSetData($tData, 1, $iChannel - 1)
	DllStructSetData($tData, 2, $iControl)
	For $i = 0 To $iParamCnt - 1
		DllStructSetData($tData, 3, $aiDests[$i][0], 2 * $i + 1)
		DllStructSetData($tData, 3, $aiDests[$i][1], 2 * $i + 2)
	Next

	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False

	Return _midi_SendSysEx($hDevice, $dMsg)

EndFunc   ;==>_midi_SetControllerDest

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetDrumChorus
; Description ...: Sets the chorus send level for a percussion instrument on devices that implement either the Roland GS or
;                  Yamaha XG standard.
; Syntax ........: _midi_SetDrumChorus($hDevice, $iChannel, $iNote[, $iLevel = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNote - The key number of the percussion instrument.
;                  $iLevel - The send level (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetDrumChorus($hDevice, $iChannel, $iNote, $iLevel = 0x40)
	Local $bResult
	If $iNote < 0 Or $iNote > 0x7F Then Return False
	If $iLevel < 0 Or $iLevel > 0x7F Then Return False
	_midi_SetNRPN($hDevice, $iChannel, $NRPN_DRUM_CHORUS_MSB, $iNote, $iLevel)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetDrumChorus

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetDrumLevel
; Description ...: Sets the volume of a percussion instrument on devices that implement either the Roland GS or Yamaha XG
;                  standard.
; Syntax ........: _midi_SetDrumLevel($hDevice, $iChannel, $iNote[, $iLevel = 0x7F])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNote - The key number of the percussion instrument.
;                  $iLevel - The send level (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SetDrumPan, _midi_SetDrumPitch, _midi_SetDrumReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetDrumLevel($hDevice, $iChannel, $iNote, $iLevel = 0x7F)
	Local $bResult
	If $iNote < 0 Or $iNote > 0x7F Then Return False
	If $iLevel < 0 Or $iLevel > 0x7F Then Return False
	_midi_SetNRPN($hDevice, $iChannel, $NRPN_DRUM_LEVEL_MSB, $iNote, $iLevel)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetDrumLevel

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetDrumPan
; Description ...: Sets the pan of a percussion instrument on devices that implement either the Roland GS or Yamaha XG
;                  standard.
; Syntax ........: _midi_SetDrumPan($hDevice, $iChannel, $iNote[, $iPan = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNote - The key number of the percussion instrument.
;                  $iPan - The position of the instrument (0, 0x01 - 0x40 - 0x7F)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $iPan is implemented as follows:
;                  0x00        = random pan
;                  0x01 - 0x7F = hard left to hard right
;                  0x40        = centered
; Related .......: _midi_SetDrumLevel, _midi_SetDrumPitch, _midi_SetDrumReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetDrumPan($hDevice, $iChannel, $iNote, $iPan = 0x40)
	Local $bResult
	If $iNote < 0 Or $iNote > 0x7F Then Return False
	If $iPan < 0 Or $iPan > 0x7F Then Return False
	_midi_SetNRPN($hDevice, $iChannel, $NRPN_DRUM_PAN_MSB, $iNote, $iPan)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetDrumPan

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetDrumPitch
; Description ...: Sets the pitch of a percussion instrument on devices that implement either the Roland GS or Yamaha XG
;                  standard.
; Syntax ........: _midi_SetDrumPitch($hDevice, $iChannel, $iNote[, $iPitch = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNote - The key number of the percussion instrument.
;                  $iPitch - The relative change to the pitch (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_SetDrumLevel, _midi_SetDrumPan, _midi_SetDrumReverb
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetDrumPitch($hDevice, $iChannel, $iNote, $iPitch = 0x40)
	Local $bResult
	If $iNote < 0 Or $iNote > 0x7F Then Return False
	If $iPitch < 0 Or $iPitch > 0x7F Then Return False
	_midi_SetNRPN($hDevice, $iChannel, $NRPN_DRUM_PITCH_CRS_MSB, $iNote, $iPitch)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetDrumPitch

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetDrumProperties
; Description ...: Sets parameters for key based instruments on rhythm channels.
; Syntax ........: _midi_SetDrumProperties($hDevice, $iChannel, $iKey, [$aiProperties = Default[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - The 1 based rhythm channel to target.
;                  $iKey - The key-based instrument to control
;                  $aiProperties - an array properties and values
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $aiProperties is 2 dimentional array defined as:
;                  $aiProperties[index][0] = Parameter ($KBC_* value)
;                  $aiProperties[index][1] = Value
;                  Properties correlate to CC values, including the use of the LSB controllers. In this context controller
;                  numbers #120 and #121 have been redefined as $KBC_COURSE_TUNING and $KBC_FINE_TUNING.
;                  The following parameter numbers and their LSB counterparts are prohibited:
;                  $CC_BANK, $CC_RPN, $CC_NRPN, $MM_OMNI_OFF, $MM_OMNI_ON, $MM_OMNI_OFF, $MM_MONO_ON, $MM_POLY_ON.
;                  The paramters and ranges below are supported in GM2 compliant devices:
;                  Parameter    | Range            | Meaning                   | Default
;                  $KBC_VOLUME  | 0 - 0x40 - 0x7F  | The relative volume       | 0x40 (no change)
;                  $KBC_PAN     | 0 - 0x40 - 0x7F  | Left - center - right pan | preset
;                  $KBC_REVERB  | 0 - 0x7F         | 0 - Max send level        | preset
;                  $KBC_CHORUS  | 0 - 0x7F         | 0 - Max send level        | preset
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetDrumProperties($hDevice, $iChannel, $iKey, $aiProperties = Default, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tBody, $pBody, $iParamCnt
	Local $aiDefProps[4][2] = [[$KBC_VOLUME, 0x40], [$KBC_PAN, 0x40], [$KBC_REVERB, 0x40], [$KBC_CHORUS, 0x40]]

	If $aiProperties = Default Then $aiProperties = $aiDefProps
	$iParamCnt = UBound($aiProperties)
	If Not $iParamCnt Then Return False
	If $iChannel < 1 Or $iChannel > 16 Then Return False
	If $iKey < 0 Or $iKey > 127 Then Return False

	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_KEYBASED_CTRL, 1, 2 * $iParamCnt + 2)
	$tBody = DllStructCreate(StringFormat("byte CHAN;byte KEY;byte[%d]", $iParamCnt * 2), $pBody)

	DllStructSetData($tBody, 1, $iChannel - 1)
	DllStructSetData($tBody, 2, $iKey)
	For $i = 0 To $iParamCnt - 1
		DllStructSetData($tBody, 3, $aiProperties[$i][0], 2 * $i + 1)
		DllStructSetData($tBody, 3, $aiProperties[$i][1], 2 * $i + 2)
	Next

	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False

	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetDrumProperties

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetDrumReverb
; Description ...: Sets the reverb send level for a percussion instrument on devices that implement either the Roland GS or
;                  Yamaha XG standard.
; Syntax ........: _midi_SetDrumReverb($hDevice, $iChannel, $iNote[, $iLevel = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNote - The key number of the percussion instrument.
;                  $iLevel - The send level (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: _midi_SetDrumLevel, _midi_SetDrumPan, _midi_SetDrumPitch
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetDrumReverb($hDevice, $iChannel, $iNote, $iLevel = 0x28)
	Local $bResult
	If $iNote < 0 Or $iNote > 0x7F Then Return False
	If $iLevel < 0 Or $iLevel > 0x7F Then Return False
	_midi_SetNRPN($hDevice, $iChannel, $NRPN_DRUM_REVERB_MSB, $iNote, $iLevel)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetDrumReverb

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetFilterCutoff
; Description ...: Changes the filter cutoff frequency on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetFilterCutoff($hDevice, $iChannel[, $iFrequency = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iFrequency - The relative change to the frequency (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iFrequency values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetFilterResonance
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetFilterCutoff($hDevice, $iChannel, $iFrequency = 0x40)
	Local $bResult
	If $iFrequency < 0 Or $iFrequency > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_FILTER_MSB, $NRPN_FILTER_FREQ, $iFrequency)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetFilterCutoff

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetFilterResonance
; Description ...: Changes the filter resonance on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetFilterResonance($hDevice, $iChannel[, $iResonance = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iResonance - The relative change to the resonance (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iResonance values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetFilterCutoff
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetFilterResonance($hDevice, $iChannel, $iResonance = 0x40)
	Local $bResult
	If $iResonance < 0 Or $iResonance > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_FILTER_MSB, $NRPN_FILTER_RESONANCE, $iResonance)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetFilterResonance

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetGMMode
; Description ...: Requests a device to change its mode of operation to best support a General Midi standard.
; Syntax ........: _midi_SetGMMode($hDevice, $iMode[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iMode - Specifies a $GM_MODE_* value
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iMode values are:
;                  $GM_MODE_OFF - Return to the device's native mode.
;                  $GM_MODE_GM1 - General Midi 1
;                  $GM_MODE_GM2 - General Midi 2
; Related .......: _midi_SendRolandGSReset, _midi_SendXGSystemOn
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetGMMode($hDevice, $iMode, $dDeviceID = $DEVID_BROADCAST)
	Local $tBuffer, $dMsg, $aiSubIDMap[3] = [2, 1, 3]
	If $iMode < 0 Or $iMode > 2 Then Return False
	__midi_BuildSysEx_Uvsl($tBuffer, $UID_NONREALTIME, $dDeviceID, $NRTID_GM, $aiSubIDMap[$iMode])
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetGMMode

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetLocalControl
; Description ...: Sends a "Local Control" channel mode message to a midi output device.
; Syntax ........: _midi_SetLocalControl($hDevice, $bState[, $iBasicCh = 1])
; Parameters ....: $hDevice - A midi output device handle.
;                  $bState - A boolean value that specifies if local control should be enabled or disabled.
;                  $iBasicCh - The device's basic channel.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetLocalControl($hDevice, $bState, $iBasicCh = 1)
	Local $bResult, $iValue = 0
	If $bState Then $iValue = 127
	$bResult = _midi_SendControlChange($hDevice, $iBasicCh, $MM_LOCAL_CTRL, $iValue)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetLocalControl

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetMidiMode
; Description ...: Requests a midi output device is set to a specified mode.
; Syntax ........: _midi_SetMidiMode($hDevice, $iMode[, $iBasicCh = 1[, $iChsToAssign = Default]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iMode - Specifies which mode should be used.
;                  $iBasicCh - The device's basic channel.
;                  $iChsToAssign - Specifies how many channels should be assigned when using Mode 4.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Possible modes are:
;                  1 - Omni On, Polyphonic
;                  2 - Omni On, Monophonic
;                  3 - Omni Off, Polyphonic (Mostly used)
;                  4 - Omni Off, Monophonic
;                  $iChsToAssign is for use with mode 4. If $iChsToAssign = 0 or Default, then all channels greater or equal
;                  to the device's basic channel should be assigned by the instrument.
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetMidiMode($hDevice, $iMode, $iBasicCh = 1, $iChsToAssign = Default)
	Local $bPoly, $bOmni

	If $iBasicCh < 1 Or $iBasicCh > 16 Then Return False
	If $iMode < 1 Or $iMode > 4 Then Return False
	$bOmni = ($iMode < 3)
	$bPoly = (BitAND($iMode, 1) = 1)
	If $iChsToAssign = Default Then $iChsToAssign = Int($bOmni = True)
	If $iChsToAssign < 0 Or $iChsToAssign > 16 Then Return False

	If $bOmni Then
		_midi_SendControlChange($hDevice, $iBasicCh, 0X7D, 0)
	Else
		_midi_SendControlChange($hDevice, $iBasicCh, 0x7C, 0)
	EndIf
	If @error Then Return SetError(@error, @extended, False)

	If $bPoly Then
		_midi_SendControlChange($hDevice, $iBasicCh, 0x7F, 0)
	Else
		_midi_SendControlChange($hDevice, $iBasicCh, 0x7E, $iChsToAssign)
	EndIf
	If @error Then Return SetError(@error, @extended, False)

	Return True
EndFunc   ;==>_midi_SetMidiMode

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetModDepthRange
; Description ...: Scales the effective range of the modulation wheel (CC #1).
; Syntax ........: _midi_SetModDepthRange($hDevice, $iChannel[, $iCourse = 0[, $iFine = 0x40]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iCourse - The range in semitones (0 - 127)
;                  $iFine - The rage in 100/128 Cents. (0 - 127)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetModDepthRange($hDevice, $iChannel, $iCourse = 0, $iFine = 0x40)
	Local $iSensitvity, $bResult
	If $iCourse < 0 Or $iCourse > 127 Then Return False
	If $iFine < 0 Or $iFine > 127 Then Return False
	$iSensitvity = BitShift($iCourse, -7) + $iFine
	$bResult = _midi_SetRPN($hDevice, $iChannel, $RPN_MOD_DEPTH_RANGE, $iSensitvity, True)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetModDepthRange

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetMstrBalance
; Description ...: Sets the master L/R balance for a device.
; Syntax ........: _midi_SetMstrBalance($hDevice[, $iBalance = 0x2000[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - a midi output device handle.
;                  $iBalance - the stereo balance (0 - 0x2000 - 0x3FFF).
;                  $dDeviceID - the device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iBalance values range from 0 (Hard Left)to 0x3FFF (Hard Right), with 0x2000 signfying the device is
;                  centered.
; Related .......: _midi_SetMstrVolume
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetMstrBalance($hDevice, $iBalance = 0x2000, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tBody, $pBody
	If $iBalance < 0 Or $iBalance > 0x3FFF Then Return False
	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_DEVCTRL, 2, 2)
	$tBody = DllStructCreate("byte;byte", $pBody)
	DllStructSetData($tBody, 1, BitAND($iBalance, 0x7F))
	DllStructSetData($tBody, 2, BitShift($iBalance, 7))
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetMstrBalance

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetMstrCourseTuning
; Description ...: Sets the master tuning for a device.
; Syntax ........: _midi_SetMstrCourseTuning($hDevice[, $iTuning = 0x40[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - a midi output device handle.
;                  $iTuning - Specifies a tuning offset in semitones. (0 - 64 - 127)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iTuning values range from 0 to 127, with a value of 64 signifying no displacement from A440.
;                  GM2 compliant devices are required to support a range of at least +/-12 semitones. (values 46 to 76).
; Related .......: _midi_SetMstrFineTuning
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetMstrCourseTuning($hDevice, $iTuning = 0x40, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tBody, $pBody
	If $iTuning < 0 Or $iTuning > 0x7F Then Return False
	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_DEVCTRL, 4, 2)
	$tBody = DllStructCreate("byte;byte", $pBody)
	DllStructSetData($tBody, 2, $iTuning)
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetMstrCourseTuning

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetMstrFineTuning
; Description ...: Sets the master tuning for a device.
; Syntax ........: _midi_SetMstrFineTuning($hDevice[, $iTuning = 0x2000[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - a midi output device handle.
;                  $iTuning - Specifies a tuning offset in 100/8192 cent increments (0 - 0x2000 - 0x3FFF)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: $iTuning is implemented as follows.
;                  minimum: 0      = -100 cents (-1 semitone)
;                  neutral: 0x2000 = No displacement from A440
;                  maximum: 0x3FFF = + 8191 * 100/8192 cents (~+1 semitone)
; Related .......: _midi_SetMstrCourseTuning
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetMstrFineTuning($hDevice, $iTuning = 0x2000, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tBody, $pBody
	If $iTuning < 0 Or $iTuning > 0x3FFF Then Return False
	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_DEVCTRL, 3, 2)
	$tBody = DllStructCreate("byte;byte", $pBody)
	DllStructSetData($tBody, 1, BitAND($iTuning, 0x7F))
	DllStructSetData($tBody, 2, BitShift($iTuning, 7))
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetMstrFineTuning

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetMstrVolume
; Description ...: Sets the master volume for a device.
; Syntax ........: _midi_SetMstrVolume($hDevice[, $iVolume = 0x2000[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - a midi output device handle.
;                  $iVolume - the stereo balance (0 - 0x3FFF).
;                  $dDeviceID - the device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iVolume values range from 0 (muted) to 0x3FFF (full volume).
; Related .......: _midi_SetMstrBalance
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetMstrVolume($hDevice, $iVolume = 0x3FFF, $dDeviceID = $DEVID_BROADCAST)
	Local $dMsg, $tBuffer, $tBody, $pBody
	If $iVolume < 0 Or $iVolume > 0x3FFF Then Return False
	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_REALTIME, $dDeviceID, $RTID_DEVCTRL, 1, 2)
	$tBody = DllStructCreate("byte;byte", $pBody)
	DllStructSetData($tBody, 1, BitAND($iVolume, 0x7F))
	DllStructSetData($tBody, 2, BitShift($iVolume, 7))
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetMstrVolume

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetNRPN
; Description ...: Sets the value of a non-registered parameter.
; Syntax ........: _midi_SetNRPN($hDevice, $iChannel, $iNRPN_MSB, $iNRPN_LSB, $iValue[, $b14Bit = False])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iNRPN_MSB - Specifies which NRPN to change.
;                  $iNRPN_LSB - Specifies which NRPN to change.
;                  $iValue - Specifies the new value of the non-registered parameter.
;                  $b14Bit - Specifies if $iValue should be treated as a 14 bit value.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: NRPNs are generally expressed as a MSB/LSB pair. If a 14 bit NRPN is specified by the manufacturer, $iNRPN_MSB
;                  represents the upper 7 bits of the NRPN while $iNRPN_LSB represents the lower 7 bits.
;                  If $b14Bit = True then $iValue is split accross controls #6 and #38, with the upper 7 bits assigned to
;                  control #6. If $b14Bit = False then the entire value of $iValue is assigned to control #6.
; Related .......: _midi_IncrementNRPN, _midi_DecrementNRPN
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetNRPN($hDevice, $iChannel, $iNRPN_MSB, $iNRPN_LSB, $iValue, $b14Bit = False)
	Local $bResult
	$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_NRPN_MSB, $iNRPN_MSB)
	If Not $bResult Then Return SetError(@error, @extended, $bResult)
	$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_NRPN_LSB, $iNRPN_LSB)
	If Not $bResult Then Return SetError(@error, @extended, $bResult)

	If $b14Bit Then
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_DATA, BitShift($iValue, 7))
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_DATA_LSB, BitAND(0x7F, $iValue))
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
	Else
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_DATA, $iValue)
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
	EndIf
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_NRPN, $RPN_NULL)
EndFunc   ;==>_midi_SetNRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetOctiveTuning
; Description ...: Sets the micro-tuning of an instrument away from equal temperment.
; Syntax ........: _midi_SetOctiveTuning($hDevice[, $aiOffset = Default[, $iChanMask = $CH_MSK_ALL[, $dDeviceID = $DEVID_BROADCAST]]])
; Parameters ....: $hDevice - a handle value.
;                  $aiOffset - A 12 element array containg a tuning offset for each note in an octive.
;                  $iChanMask - A 16 bit mask representing the midi channels to alter.
;                  $dDeviceID - The target device ID.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: This function sends a non real time, "scale/octave tuning" SysEx message in its 1-byte form.
;                  $CH_MSK_* values may be combined with BitOr to target specific channels for the $iChanMask parameter.
;                  $aiOffset[0] represents C, with each element representing a note through to $aiOffset[11], which is B.
;                  The value of each element is a 7bit number representing the shift away from an instrument's tuning preset in
;                  cents.
;                  0          = -64 cents
;                  64  (0x40) = No shift (Ususally from equal temperment)
;                  127 (0x7F) = +63 cents
; Related .......:
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetOctiveTuning($hDevice, $aiOffset = Default, $iChanMask = $CH_MSK_ALL, $dDeviceID = $DEVID_BROADCAST)
	Local $aiDefOffset[12], $dMsg, $tBuffer, $tBody, $pBody

	If $aiOffset = Default Then
		$aiOffset = $aiDefOffset
		For $i = 0 To 11
			$aiOffset[$i] = 0x40
		Next
	ElseIf UBound($aiOffset) <> 12 Then
		Return False
	EndIf
	If $iChanMask < 0 Or $iChanMask > 0xFFFF Then Return False

	$pBody = __midi_BuildSysEx_Uvsl($tBuffer, $UID_NONREALTIME, $dDeviceID, $NRTID_TUNING_STD, 8, 15)
	$tBody = DllStructCreate("byte[3];byte[12]", $pBody)

	For $i = 3 To 1 Step -1
		DllStructSetData($tBody, 1, BitAND($iChanMask, 0x7F), $i)
		$iChanMask = BitShift($iChanMask, 7)
	Next
	For $i = 0 To 11
		If $aiOffset[$i] < 0 Or $aiOffset[$i] > 0x7F Then Return False
		DllStructSetData($tBody, 2, $aiOffset[$i], $i + 1)
	Next
	$dMsg = DllStructGetData($tBuffer, 1)
	If Not IsBinary($dMsg) Then Return False
	Return _midi_SendSysEx($hDevice, $dMsg)
EndFunc   ;==>_midi_SetOctiveTuning

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetPitchBendSens
; Description ...: Sets the effective range of the Pitch Bend controller (CC #0)
; Syntax ........: _midi_SetPitchBendSens($hDevice, $iChannel[, $iCourse = 2[, $iFine = 0]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - The midi channel that is affected
;                  $iCourse - Range in semitones. (0 - 127)
;                  $iFine - Range in cents (0 - 127)
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......: It is not uncommon for instruments to ignore the $iFine parameter.
;                  This function will internally wrap the $iFine value into $iCourse where possible if it exceeds 99 cents.
; Related .......: _midi_SendPitchBend
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetPitchBendSens($hDevice, $iChannel, $iCourse = 2, $iFine = 0)
	Local $iSensitvity, $bResult
	If $iFine < 0 Or $iFine > 127 Then Return False
	If $iCourse < 127 Then
		$iCourse += Floor($iFine / 100)
		$iFine = Mod($iFine, 100)
	EndIf
	If $iCourse < 0 Or $iCourse > 127 Then Return False

	$iSensitvity = BitShift($iCourse, -7) + $iFine
	$bResult = _midi_SetRPN($hDevice, $iChannel, $RPN_BEND_SENS, $iSensitvity, True)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetPitchBendSens

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetReverbTime
; Description ...: Sets the time parameter of the reverb unit on General Midi 2 compliant devices.
; Syntax ........: _midi_SetReverbTime($hDevice, $iTime[, $dDeviceID = $DEVID_BROADCAST])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iTime - Specifies the decay time of the reverb effect. (0 - 127)
;                  $dDeviceID - The device ID to target.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: The time parameter can be calculated by:
;                  Time for the low frequencies to fall by -60 dB in seconds = ln($iTime) / 0.025 + 40
; Related .......: _midi_SetReverbType
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetReverbTime($hDevice, $iTime, $dDeviceID = $DEVID_BROADCAST)
	If $iTime < 0 Or $iTime > 0x7F Then Return False
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_RVB, $RVBP_TIME, Binary(Chr($iTime)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetReverbTime

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetReverbType
; Description ...: Sets the type of reverb on General Midi 2 compliant devices.
; Syntax ........: _midi_SetReverbType($hDevice[, $iType = $RVB_LARGE_HALL[, $dDeviceID = $DEVID_BROADCAST]])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iPreset - Specifies the chorus type.
;                  $dDeviceID - [optional] a binary variant value. Default is $DEVID_BROADCAST.
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid reverb types and thier default time parameters are listed below:
;                  Preset           | Time
;                  $RVB_SMALL_ROOM  | 44 (0x2C) - 1.1s
;                  $RVB_MEDUIM_ROOM | 50 (0x32) - 1.3s
;                  $RVB_LARGE_ROOM  | 56 (0x38) - 1.5s
;                  $RVB_SMALL_HALL  | 64 (0x40) - 1.8s
;                  $RVB_LARGE_HALL  | 64 (0x40) - 1.8s
;                  $RVB_PLATE       | 50 (0x32) - 1.3s
; Related .......: _midi_SetReverbTime
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetReverbType($hDevice, $iType = $RVB_LARGE_HALL, $dDeviceID = $DEVID_BROADCAST)
	If $iType < 0 Or $iType > 4 Then
		If $iType <> 8 Then Return False
	EndIf
	Local $dSysEx = __midi_BuildGblParCtrlMsg($SLPTH_RVB, $RVBP_TYPE, Binary(Chr($iType)), $dDeviceID)
	Return _midi_SendSysEx($hDevice, $dSysEx)
EndFunc   ;==>_midi_SetReverbType

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetRPN
; Description ...: Sets the value of a registered parameter.
; Syntax ........: _midi_SetRPN($hDevice, $iChannel, $iRPN, $iValue[, $b14Bit = False])
; Parameters ....: $hDevice - A midi output device handle.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iRPN - Specifies which RPN to change.
;                  $iValue - Specifies the new value of the registered parameter.
;                  $b14Bit - Specifies if $iValue should be treated as a 14 bit value.
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......: _midi_IncrementRPN, _midi_DecrementRPN
; Remarks .......: If $b14Bit = True then $iValue is split accross controls #6 and #38, with the upper 7 bits assigned to
;                  control #6. If $b14Bit = False then the entire value of $iValue is assigned to control #6.
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetRPN($hDevice, $iChannel, $iRPN, $iValue, $b14Bit = False)
	Local $bResult
	$bResult = _midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $iRPN)
	If Not $bResult Then Return SetError(@error, @extended, $bResult)
	If $b14Bit Then
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_DATA, BitShift($iValue, 7))
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_DATA_LSB, BitAND(0x7F, $iValue))
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
	Else
		$bResult = _midi_SendControlChange($hDevice, $iChannel, $CC_DATA, $iValue)
		If Not $bResult Then Return SetError(@error, @extended, $bResult)
	EndIf
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, 0x3FFF)
EndFunc   ;==>_midi_SetRPN

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetVibratoDelay
; Description ...: Changes the vibrato delay parameter on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetVibratoDelay($hDevice, $iChannel[, $iDelay = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iDelay - The vibrato delay relative change (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iDelay values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetVibratoRate, _midi_SetVibratoDepth
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetVibratoDelay($hDevice, $iChannel, $iDelay = 0x40)
	Local $bResult
	If $iDelay < 0 Or $iDelay > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_VIBRATO_MSB, $NRPN_VIBRATO_DELAY, $iDelay)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetVibratoDelay

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetVibratoDepth
; Description ...: Changes the vibrato depth parameter on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetVibratoDepth($hDevice, $iChannel[, $iDepth = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iDepth - The vibrato depth relative change (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iDepth values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetVibratoRate, _midi_SetVibratoDelay
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetVibratoDepth($hDevice, $iChannel, $iDepth = 0x40)
	Local $bResult
	If $iDepth < 0 Or $iDepth > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_VIBRATO_MSB, $NRPN_VIBRATO_DEPTH, $iDepth)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetVibratoDepth

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_SetVibratoRate
; Description ...: Changes the vibrato rate parameter on devices that implement either the Roland GS or Yamaha XG standard.
; Syntax ........: _midi_SetVibratoRate($hDevice, $iChannel[, $iRate = 0x40])
; Parameters ....: $hDevice - a handle value.
;                  $iChannel - Specifies which midi channel is affected.
;                  $iRate - The vibrato rate relative change (0x40 = no change)
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......:
; Remarks .......: Valid $iRate values vary between the two standards. These are outlined below:
;                  Standard  (minimum - no change - maximum)
;                  Roland GS (0x0E - 0x40 - 0x72)
;                  Yamaha XG (0x00 - 0x40 - 0x7F)
; Related .......: _midi_SetVibratoDepth, _midi_SetVibratoDelay
; Link ..........:
; Example .......: Yes
; ===============================================================================================================================
Func _midi_SetVibratoRate($hDevice, $iChannel, $iRate = 0x40)
	Local $bResult
	If $iRate < 0 Or $iRate > 0x7F Then Return False
	$bResult = _midi_SetNRPN($hDevice, $iChannel, $NRPN_VIBRATO_MSB, $NRPN_VIBRATO_RATE, $iRate)
	Return SetError(@error, @extended, $bResult)
EndFunc   ;==>_midi_SetVibratoRate

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_Shutdown
; Description ...: Reciprical fuction to _midi_Startup.
; Syntax ........: _midi_Shutdown()
; Parameters ....: None
; Return values .: None
; Author ........: MattyD
; Modified ......:
; Remarks .......:
; Related .......: _midi_Startup
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func _midi_Shutdown()
	_midiAPI_Shutdown()
	AdlibUnRegister("__midi_CleanQueues")
	GUIRegisterMsg($MM_MIM_LONGDATA, "")
	GUIRegisterMsg($MM_MIM_DATA, "")
	GUIRegisterMsg($MM_MIM_OPEN, "")
	GUIRegisterMsg($MM_MIM_CLOSE, "")
	GUIRegisterMsg($MM_MOM_OPEN, "")
	GUIRegisterMsg($MM_MOM_CLOSE, "")
EndFunc   ;==>_midi_Shutdown

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_Startup
; Description ...: Initialises a script for use with midi functions.
; Syntax ........: _midi_Startup()
; Parameters ....: None
; Return values .: Success: True
;                  Failure: False
; Author ........: MattyD
; Modified ......: Peter Verbeek
; Remarks .......: Window messages handling moved to this function so midi can be stopped and restarted again
; Related .......: _midi_Shutdown
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func _midi_Startup()
	If Not _midiAPI_Startup() Then Return False
	GUIRegisterMsg($MM_MIM_DATA, "__MM_MIM_DATA")
	GUIRegisterMsg($MM_MIM_LONGDATA, "__MM_MIM_LONGDATA")
	GUIRegisterMsg($MM_MIM_OPEN, "__MM_STATE_CHANGE")
	GUIRegisterMsg($MM_MIM_CLOSE, "__MM_STATE_CHANGE")
	GUIRegisterMsg($MM_MOM_OPEN, "__MM_STATE_CHANGE")
	GUIRegisterMsg($MM_MOM_CLOSE, "__MM_STATE_CHANGE")
	Return (AdlibRegister("__midi_CleanQueues", 1800) = True)
EndFunc   ;==>_midi_Startup

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_UnpackAddress
; Description ...: Converts an binary representation of an address into a integer.
; Syntax ........: _midi_UnpackAddress($dAddress)
; Parameters ....: $dAddress - A binary representation of an address.
; Return values .: An integer
; Author ........: MattyD
; Modified ......:
; Remarks .......: For use with SysEx functions that require an address parameter.
; Related .......: _midi_PackAddress
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func _midi_UnpackAddress($dAddress)
	Local $tBin, $iAddress, $iLength
	$iLength = BinaryLen($dAddress)
	$tBin = DllStructCreate(StringFormat("byte[%d]", $iLength))
	DllStructSetData($tBin, 1, $dAddress)
	For $i = 1 To $iLength
		$iAddress = BitShift($iAddress, -8)
		$iAddress += DllStructGetData($tBin, 1, $i)
	Next
	Return $iAddress
EndFunc   ;==>_midi_UnpackAddress

; #FUNCTION# ====================================================================================================================
; Name ..........: _midi_UnpackSize
; Description ...: Converts a binary representation of a size parameter into a integer.
; Syntax ........: _midi_UnpackSize($dSize)
; Parameters ....: $dSize - A binary representation of a size
; Return values .: An integer
; Author ........: MattyD
; Modified ......:
; Remarks .......: For use with SysEx functions that require a size parameter.
; Related .......: _midi_PackSize
; Link ..........:
; Example .......: No
; ===============================================================================================================================
Func _midi_UnpackSize($dSize)
	Local $tBin, $iSize, $iLength
	$iLength = BinaryLen($dSize)
	$tBin = DllStructCreate(StringFormat("byte[%d]", $iLength))
	DllStructSetData($tBin, 1, $dSize)

	For $i = 1 To $iLength
		$iSize = BitShift($iSize, -7)
		$iSize += DllStructGetData($tBin, 1, $i)
	Next
	Return $iSize
EndFunc   ;==>_midi_UnpackSize
