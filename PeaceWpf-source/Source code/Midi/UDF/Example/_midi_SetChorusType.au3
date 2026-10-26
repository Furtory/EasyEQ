#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $iChannel = 1

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(SelDevice(), $hGUI)

	_midi_SendControlChange($hDevice, $iChannel, $CC_CHORUS, 127)
	PlayNotes($hDevice, $iChannel)

	MsgBox(0, "GM2 Global Param Ctrl", "Press OK to select a chorus type (if supported)")
	_midi_SetChorusType($hDevice, $CHS_FLANGER)
	PlayNotes($hDevice, $iChannel)

	_midi_SendControlChange($hDevice, $iChannel, $CC_CHORUS, 0)
	_midi_SetChorusType($hDevice)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func PlayNotes($hDevice, $iChannel)
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $iVelocity = 0x60

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], $iVelocity)
		Sleep(200)
	Next

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(1500)
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
