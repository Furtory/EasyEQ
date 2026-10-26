#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hTimer, $iTimeout = 500
	Local $asDevices, $hInDevice, $hOutDevice
	Local $adDT1, $sRetMsg, $dSysEx
	Local $sReport = "Model:\t[%s]\nAddress:\t[%s]\nData:\t[%s]\n"

	;Model
	Local Const $MODEL_V1HD = Binary("0x00000020")

	;Address & Size
	Local Const $V1HD_ADD_AUDP1 = 0x710100 ;"Audio Parameter-1" area
	Local Const $AUD_EQ_LEN = 7
	Local $aiEQ1Offsets[4] = [0x08, 0x12, 0x1C, 0x26]
	Local $dEQ3Address, $dParamSize

	$dEQ3Address = _midi_PackAddress($V1HD_ADD_AUDP1 + $aiEQ1Offsets[2])
	$dParamSize = _midi_PackSize($AUD_EQ_LEN)

	_midi_Startup()
	$hGUI = GUICreate("Dummy")
	$asDevices = _midi_EnumInputs()
	If Not @error Then $hInDevice = _Midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then $asDevices = _midi_EnumOutputs()
	If Not @error Then $hOutDevice = _Midi_OpenOutput(SelDevice($asDevices, "Output:"), $hGUI)
	If Not @error Then

		_midi_SendRolandRQ1($hOutDevice, $MODEL_V1HD, $dEQ3Address, $dParamSize)

		$hTimer = TimerInit()
		Do
			If _midi_ReadSysEx($hInDevice, $dSysEx) Then
				$adDT1 = _midi_ReadRolandDT1($dSysEx, 3)
				If Not @error Then ExitLoop
			EndIf
		Until TimerDiff($hTimer) > $iTimeout

		If Not BinaryLen($dSysEx) Then
			$sRetMsg = "No response was recieved."
		Else
			$sRetMsg = StringFormat($sReport,  _
					$adDT1[0], _  ; Model
					$adDT1[1], _  ; Address
					$adDT1[2])    ; Data
		EndIf

		MsgBox(0, "DT1",  $sRetMsg)

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
