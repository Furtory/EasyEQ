#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hTimer, $iTimeout = 500
	Local $asDevices, $hInDevice, $hOutDevice
	Local $adDT1, $sRetMsg, $dSysEx
	Local $sReport = "Model:\t[%s]\nAddress:\t[%s]\nData:\t[%s]\n"

	;Below holds a table of addresses & the size of various properties for the device.
	;The length of the binary values are important for constrcting the sysex message -
	;the doco for this device specifies the elements are 3 bytes a peice.

	;The size element is also taken verbatim. For midi the high bit of every data byte must be 0.
	;So a 128 byte request for this model (if supported) would be Binary("0x00017F")

	;Model
	Local Const $MODEL_V1HD = Binary("0x00000020")

	;Address, Size
	Local Const $V1HD_ADDRESS_TABLE[4][2] = [ _
		[Binary("0x700000"), Binary("0x000008")], _
		[Binary("0x700010"), Binary("0x000001")], _
		[Binary("0x701000"), Binary("0x000043")], _
		[Binary("0x701000"), Binary("0x000002")]]

	;Indicies
	Local Enum $V1HD_SYSVERS, $V1HD_SYSMODE, $V1HD_SETUP_BLOCK, $V1HD_BPM ;etc...

	_midi_Startup()
	$hGUI = GUICreate("Dummy")
	$asDevices = _midi_EnumInputs()
	If Not @error Then $hInDevice = _Midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then $asDevices = _midi_EnumOutputs()
	If Not @error Then $hOutDevice = _Midi_OpenOutput(SelDevice($asDevices, "Output:"), $hGUI)
	If Not @error Then

		_midi_SendRolandRQ1($hOutDevice, $MODEL_V1HD, _
			$V1HD_ADDRESS_TABLE[$V1HD_SYSVERS][0], _
			$V1HD_ADDRESS_TABLE[$V1HD_SYSVERS][1])

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
