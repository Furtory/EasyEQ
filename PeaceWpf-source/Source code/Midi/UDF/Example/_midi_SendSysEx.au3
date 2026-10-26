#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hTimer, $iTimeout = 500
	Local $asDevices, $hInDevice, $hOutDevice
	Local $dSysEx
	Local Const $dIDRequest = Binary("0xF07E7F0601F7")

	_midi_Startup()
	$hGUI = GUICreate("Dummy")

	$asDevices = _midi_EnumInputs()
	If Not @error Then $hInDevice = _Midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then $asDevices = _midi_EnumOutputs()
	If Not @error Then $hOutDevice = _Midi_OpenOutput(SelDevice($asDevices, "Output:"), $hGUI)
	If Not @error Then

		_midi_SendSysEx($hOutDevice, $dIDRequest)

		$hTimer = TimerInit()
		Do
			If _midi_ReadSysEx($hInDevice, $dSysEx) Then
				ExitLoop
			EndIf
		Until TimerDiff($hTimer) > $iTimeout

		If BinaryLen($dSysEx) Then
			MsgBox(0, "ID Reply", $dSysEx)
		Else
			MsgBox(0, "ID Reply",  "No response was recieved.")
		EndIf

	Else
		MsgBox(0, "Error", "A input or output device is unavailable.")
	EndIf

	_Midi_CloseInput($hInDevice)
	_Midi_CloseOutput($hOutDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func SelDevice($asDevices, $sPrompt)
	Local $sDevList, $sDefaultDev
	Local $hCombo, $hOK, $iDevID

	For $i = 0 To UBound($asDevices) -1
		$sDevList &= $asDevices[$i] & "|"
	Next
	$sDefaultDev = $asDevices[$i-1]

	GUICreate("Select Device", 270, 100)
	GUICtrlCreateLabel($sPrompt, 14, 14, 56, 21, $SS_CENTERIMAGE)
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
