#include <GUIConstants.au3>
#include <midiApi.au3>

;buffer store actions
Global Enum $BUFF_REMOVE, $BUFF_ADD, $BUFF_READ, $BUFF_COUNT

;Input buffer settings.
Global $iInBufferCnt = 2, $iInBufferSz = 512
;Feed buffers back to the driver after we've processed them.
Global $bFeedInput = True

;Gui control IDs
Global $hGUI, $hCbo_InDevs, $hCbo_OutDevs, $hBtn_OpenDevs, $hBtn_CloseDevs
Global $hEdt_OutMsg, $hEdt_InMsg, $hBtn_Send, $hBtn_ClearInMsg, $hLbl_Status

Main()

Func Main()
	MsgBox(0, "SysEx Messaging", _
			"This demonstration requires a midi input device." & @CRLF & @CRLF & _
			"Most devices should respond to a device inquiry message, but not all. " & @CRLF & _
			"If there is no response try a different midi device.")

	Local $hMIDev, $hMODev
	Local $iSelDevID

	_midiAPI_Startup()

	;Setup GUI
	GenerateGUI()
	EnumOutputs()
	EnumInputs()

	;Register callback messages
	GUIRegisterMsg($MM_MOM_OPEN, "devOpenClose")
	GUIRegisterMsg($MM_MOM_CLOSE, "devOpenClose")
	GUIRegisterMsg($MM_MIM_OPEN, "devOpenClose")
	GUIRegisterMsg($MM_MIM_CLOSE, "devOpenClose")

	GUIRegisterMsg($MM_MOM_DONE, "devLongMsgEvent")
	GUIRegisterMsg($MM_MIM_LONGDATA, "devLongMsgEvent")
	GUIRegisterMsg($MM_MIM_MOREDATA, "devLongMsgEvent")

	While 1
		Switch GUIGetMsg()

			Case $GUI_EVENT_CLOSE
				CloseDevs($hMODev, $hMIDev)
				ExitLoop

			Case $hBtn_CloseDevs
				CloseDevs($hMODev, $hMIDev)

			Case $hBtn_OpenDevs
				$iSelDevID = GUICtrlSendMsg($hCbo_OutDevs, $CB_GETCURSEL, 0, 0) - 1
				If $iSelDevID > -1 Then
					$hMODev = _midiAPI_OutOpen($iSelDevID, $hGUI, 0, $CALLBACK_WINDOW)
					If @error Then GUICtrlSetData($hLbl_Status, _midiAPI_OutGetErrorText(@error))
				EndIf

				$iSelDevID = GUICtrlSendMsg($hCbo_InDevs, $CB_GETCURSEL, 0, 0) - 1
				If $iSelDevID > -1 Then
					$hMIDev = _midiAPI_InOpen($iSelDevID, $hGUI, 0, $CALLBACK_WINDOW)
					If @error Then GUICtrlSetData($hLbl_Status, _midiAPI_InGetErrorText(@error))
				EndIf

			Case $hBtn_Send
				GUICtrlSetData($hEdt_InMsg, "")
				SendLongMsg($hMODev, Binary(GUICtrlRead($hEdt_OutMsg)))

			Case $hBtn_ClearInMsg
				GUICtrlSetData($hEdt_InMsg, "")
		EndSwitch
	WEnd

	GUIDelete($hGUI)
	_midiAPI_Shutdown()
EndFunc   ;==>Main


Func GenerateGUI()
	;Create the GUI
	$hGUI = GUICreate("SysEx Messaging", 380, 166)

	GUICtrlCreateLabel("Midi Out:", 4, 14, 50, 24, BitOR($SS_CENTERIMAGE, $SS_RIGHT))
	$hCbo_OutDevs = GUICtrlCreateCombo("[None]", 58, 15, 120, 25, $CBS_DROPDOWNLIST)
	$hCbo_InDevs = GUICtrlCreateCombo("[None]", 58, 41, 120, 25, $CBS_DROPDOWNLIST)
	$hBtn_OpenDevs = GUICtrlCreateButton("Open", 182, 14, 42, 50)
	$hBtn_CloseDevs = GUICtrlCreateButton("Close", 225, 14, 42, 50)
	GUICtrlSetState(-1, $GUI_DISABLE)

	GUICtrlCreateLabel("ID Req:", 4, 84, 50, 21, BitOR($SS_CENTERIMAGE, $SS_RIGHT))
	$hEdt_OutMsg = GUICtrlCreateInput("0xF07E7F0601F7", 58, 84, 120, 21)
	GUICtrlSetState($hEdt_OutMsg, $GUI_DISABLE)

	$hBtn_Send = GUICtrlCreateButton("Send >>", 182, 84, 85, 21)
	GUICtrlSetState(-1, $GUI_DISABLE)

	GUICtrlCreateLabel("Resp:", 4, 110, 50, 21, BitOR($SS_CENTERIMAGE, $SS_RIGHT))
	$hEdt_InMsg = GUICtrlCreateInput("", 58, 110, 290, 21)
	GUICtrlSetState(-1, $GUI_DISABLE)
	$hBtn_ClearInMsg = GUICtrlCreateButton("X", 349, 110, 21, 21)

	$hLbl_Status = GUICtrlCreateLabel( _
			"Set the midi in && out ports to the same physical device.", _
			0, 140, 380, 20, BitOR($SS_CENTERIMAGE, $SS_CENTER))

	GUISetState()
EndFunc   ;==>GenerateGUI

Func EnumInputs()
	Local $tCaps, $sDevice, $sDevList

	;Enum output devices
	$tCaps = DllStructCreate($tag_midioutcaps)
	For $i = 0 To _midiAPI_OutGetNumDevs() -1
		_midiAPI_OutGetDevCaps($i, DllStructGetPtr($tCaps))
		$sDevice = DllStructGetData($tCaps, "szPname")
		$sDevList &= $sDevice & "|"
	Next
	If $sDevice Then GUICtrlSetData($hCbo_OutDevs, $sDevList, $sDevice)
EndFunc   ;==>EnumInputs

Func EnumOutputs()
	Local $tCaps, $sDevice, $sDevList

	;Enum input devices
	$tCaps = DllStructCreate($tag_midiincaps)
	For $i = 0 To _midiAPI_InGetNumDevs() -1
		_midiAPI_InGetDevCaps($i, DllStructGetPtr($tCaps))
		$sDevice = DllStructGetData($tCaps, "szPname")
		$sDevList &= $sDevice & "|"
	Next
	If $sDevice Then GUICtrlSetData($hCbo_InDevs, $sDevList, $sDevice)
EndFunc   ;==>EnumOutputs

Func CreateBuffer(ByRef $pMidiHdr, ByRef $tMidiHdr, ByRef $tBuffer, $iSize)
	;Create a generic buffer & header
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iSize))
	$tMidiHdr = DllStructCreate($tag_midihdr)
	$pMidiHdr = DllStructGetPtr($tMidiHdr)
	DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
	DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))
EndFunc   ;==>CreateBuffer

Func BufferStore($iAction, $pMidiHdr = 0, $tMidiHdr = 0, $tBuffer = 0)
	Local $iIndex, $tData, $pData, $iDataSz

	;$aBuffers[$index][$pMidiHdr, $tMidiHdr, $tBuffer]
	Local Static $aBuffers[0][3], $iBuffCount

	;We should also store the device handle associated with a buffer.
	;This would allow us to clean up after a device if it disappears unexpectedly.

	Switch $iAction
		Case $BUFF_ADD
			;Find a free slot, or extend the array if need be.
			For $i = 0 To UBound($aBuffers) - 1
				If Not $aBuffers[$i][0] Then ExitLoop
			Next
			If $i = UBound($aBuffers) Then ReDim $aBuffers[$i + 5][3]
			$iIndex = $i

			;store the buffer
			$aBuffers[$iIndex][0] = $pMidiHdr
			$aBuffers[$iIndex][1] = $tMidiHdr
			$aBuffers[$iIndex][2] = $tBuffer

			$iBuffCount += 1

		Case $BUFF_READ, $BUFF_REMOVE
			;Locate a buffer
			For $i = 0 To UBound($aBuffers) - 1
				If $aBuffers[$i][0] = $pMidiHdr Then ExitLoop
			Next
			If $i = UBound($aBuffers) Then Return
			$iIndex = $i

			If $iAction = $BUFF_READ Then

				;Read a buffer.
				$tMidiHdr = $aBuffers[$iIndex][1]
				$pData = DllStructGetData($tMidiHdr, "lpData")
				$iDataSz = DllStructGetData($tMidiHdr, "dwBytesRecorded")
				$tData = DllStructCreate(StringFormat("byte[%d]", $iDataSz), $pData)
				Return DllStructGetData($tData, 1)

			Else
				;Free the buffer.
				$aBuffers[$iIndex][0] = 0
				$aBuffers[$iIndex][1] = 0
				$aBuffers[$iIndex][2] = 0

				$iBuffCount -= 1
			EndIf

		Case $BUFF_COUNT
			;Return the number of buffers in the store.
			Return $iBuffCount

	EndSwitch
EndFunc   ;==>BufferStore

Func SendLongMsg($hDevice, $dData)

	Local $pMidiHdr, $tMidiHdr, $tBuffer

	;Just create these output buffers on the fly.
	;If we were streaming/dumping a lot of data we'd handle this differently.
	CreateBuffer($pMidiHdr, $tMidiHdr, $tBuffer, BinaryLen($dData))
	BufferStore($BUFF_ADD, $pMidiHdr, $tMidiHdr, $tBuffer)

	;Fill and prepare.
	DllStructSetData($tBuffer, 1, $dData)
	_midiAPI_OutPrepareHeader($hDevice, $pMidiHdr)

	;Send the message
	_midiAPI_OutLongMsg($hDevice, $pMidiHdr)
	If @error Then GUICtrlSetData($hLbl_Status, _midiAPI_OutGetErrorText(@error))
EndFunc   ;==>SendLongMsg

Func CloseDevs($hMODev, $hMIDev)

	GUICtrlSetState($hBtn_CloseDevs, $GUI_DISABLE)

	;Don't requeue input buffers once they've returned to us.
	$bFeedInput = False

	;Return all buffers.
	;(The midiOut buffer should already be back)
	_midiAPI_OutReset($hMODev)
	_midiAPI_InReset($hMIDev)

	;Wait until we've unprepared and freed everything before closing handles.
	;We should build in a failsafe here for devices that havn't left gracefully.
	While BufferStore($BUFF_COUNT) > 0
		Sleep(10)
	WEnd

	;Close the devices.
	_midiAPI_OutClose($hMODev)
	_midiAPI_InClose($hMIDev)
EndFunc   ;==>CloseDevs

Func SetupInputBuffer($hDevice)
	Local $pMidiHdr, $tMidiHdr, $tBuffer

	;Flag reset
	$bFeedInput = True

	;Create input buffers.
	For $i = 1 To $iInBufferCnt
		CreateBuffer($pMidiHdr, $tMidiHdr, $tBuffer, $iInBufferSz)
		BufferStore($BUFF_ADD, $pMidiHdr, $tMidiHdr, $tBuffer)
		_midiAPI_InPrepareHeader($hDevice, $pMidiHdr)

		;Queue them up.
		_midiAPI_InAddBuffer($hDevice, $pMidiHdr)
		If @error Then
			GUICtrlSetData($hLbl_Status, _midiAPI_InGetErrorText(@error))
			ExitLoop
		EndIf
	Next

	;Start listening
	_midiAPI_InStart($hDevice)
EndFunc   ;==>SetupInputBuffer

;Devices opening & closing.
Func devOpenClose($hWnd, $iMsg, $hDevice, $lParam)
	#forceref $hWnd, $iMsg, $hDevice, $lParam

	Local Static $bMomOpen, $bMimOpen

	Switch $iMsg
		Case $MM_MOM_OPEN
			$bMomOpen = True
			GUICtrlSetState($hCbo_OutDevs, $GUI_DISABLE)
			GUICtrlSetState($hBtn_OpenDevs, $GUI_DISABLE)
			GUICtrlSetState($hBtn_CloseDevs, $GUI_ENABLE)
			If $bMimOpen Then GUICtrlSetState($hBtn_Send, $GUI_ENABLE)

		Case $MM_MOM_CLOSE
			$bMomOpen = False
			GUICtrlSetState($hCbo_OutDevs, $GUI_ENABLE)
			GUICtrlSetState($hBtn_OpenDevs, $GUI_ENABLE)
			GUICtrlSetState($hBtn_Send, $GUI_DISABLE)
			GUICtrlSetData($hLbl_Status, "Set the midi in && out ports to the same physical device.")

		Case $MM_MIM_OPEN
			$bMimOpen = True
			GUICtrlSetState($hCbo_InDevs, $GUI_DISABLE)
			GUICtrlSetState($hBtn_OpenDevs, $GUI_DISABLE)
			GUICtrlSetState($hBtn_CloseDevs, $GUI_ENABLE)
			If $bMomOpen Then GUICtrlSetState($hBtn_Send, $GUI_ENABLE)
			GUICtrlSetData($hEdt_InMsg, "")

			SetupInputBuffer($hDevice)

		Case $MM_MIM_CLOSE
			$bMimOpen = False
			GUICtrlSetState($hCbo_InDevs, $GUI_ENABLE)
			GUICtrlSetState($hBtn_OpenDevs, $GUI_ENABLE)
			GUICtrlSetState($hBtn_Send, $GUI_DISABLE)
			GUICtrlSetData($hLbl_Status, "Set the midi in && out ports to the same physical device.")

	EndSwitch

EndFunc   ;==>devOpenClose

;Long message callbacks
Func devLongMsgEvent($hWnd, $iMsg, $hDevice, $pBuffHdr)
	#forceref $hWnd, $iMsg, $hDevice, $pBuffHdr

	Local $dResponse

	Switch $iMsg
		Case $MM_MOM_DONE
			;The driver is done sending our message. we should free the buffer
			_midiAPI_OutUnprepareHeader($hDevice, $pBuffHdr)
			BufferStore($BUFF_REMOVE, $pBuffHdr)
			GUICtrlSetState($hBtn_Send, $GUI_ENABLE)

		Case $MM_MIM_LONGDATA
			If $bFeedInput Then
				;Response has arrived. process the data.
				$dResponse = BufferStore($BUFF_READ, $pBuffHdr)
				GUICtrlSetData($hEdt_InMsg, $dResponse)

				;Done processing. Requeue the buffer for more input.
				_midiAPI_InAddBuffer($hDevice, $pBuffHdr)
			Else

				;Prepare for closure... delete buffers as the driver returns them.
				_midiAPI_InUnprepareHeader($hDevice, $pBuffHdr)
				BufferStore($BUFF_REMOVE, $pBuffHdr)
			EndIf
	EndSwitch
EndFunc   ;==>devLongMsgEvent
