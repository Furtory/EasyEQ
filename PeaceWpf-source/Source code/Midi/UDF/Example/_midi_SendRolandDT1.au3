#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice
	Local Const $GS_A_RESET = Binary("0x40007F")

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _midi_OpenOutput(SelDevice(), $hGUI)

	MsgBox(0, "Send DT1", "Press Ok to send a 'GS Reset' message")
	_midi_SendRolandDT1($hDevice, $MODEL_RLND_GS, $GS_A_RESET, Chr(0))
	MsgBox(0, "Send DT1", "Message Sent")
	Sleep(500)

	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func SelDevice()
	Local $asDevices, $sDevList, $sDefaultDev
	Local $hCombo, $hOK, $iDevID

	$asDevices = _midi_EnumOutputs()
	For $i = 0 To UBound($asDevices) -1
		If Not $i Then $sDefaultDev = $asDevices[$i]
		$sDevList &= $asDevices[$i] & "|"
	Next

	GUICreate("Select Device", 270, 100)
	GUICtrlCreateLabel("Output", 14, 14, 56, 21, $SS_CENTERIMAGE)
	$hCombo = GUICtrlCreateCombo("", 74, 14, 182, 25, $CBS_DROPDOWNLIST)
	GUICtrlSetData($hCombo, $sDevList, $sDefaultDev)
	$hOK = GUICtrlCreateButton("Select", 176, 61, 80, 25, $BS_DEFPUSHBUTTON)
	GUISetState()

	While 1
		Switch GUIGetMsg()
			Case $hOK, $GUI_EVENT_CLOSE
				ExitLoop
		EndSwitch
	WEnd
	$iDevID = GUICtrlSendMsg($hCombo, $CB_GETCURSEL, 0, 0)
	GUIDelete()

	Return $iDevID
EndFunc
