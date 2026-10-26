#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $iChannel = 10

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(SelDevice(), $hGUI)

	MsgBox(0, "GS/XG Drum Params", "Press OK to pan the drum (if supported)")
	For $i = 1 To 0x7F Step 8
		_midi_SetDrumPan($hDevice, $iChannel, $NTE_HIGH_BONGO, $i)
		PlayDrum($hDevice, $NTE_HIGH_BONGO)
	Next
	Sleep(500)

	_midi_SetDrumPan($hDevice, $iChannel, $NTE_HIGH_BONGO)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func PlayDrum($hDevice, $iNote)
	Local Const $RTHM_CHAN = 10
	_midi_SendNoteOn($hDevice, $RTHM_CHAN, $iNote, 0x60)
	Sleep(100)
	_midi_SendNoteOff($hDevice, $RTHM_CHAN, $iNote)
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
