#include <GUIConstants.au3>
#include <midiApi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice

	$hGUI = GUICreate("Midi Input", 220, 500)
	GUICtrlCreateLabel("Close the window to finish", 4, 4, 292, 20)
	GUICtrlCreateEdit("", 4, 28, 212, 468)
	GUIRegisterMsg($MM_MIM_OPEN, "MidiMsg")
	GUIRegisterMsg($MM_MIM_DATA, "MidiMsg")
	GUISetState()

	_midiAPI_Startup()

	If _midiAPI_InGetNumDevs() Then
		$hDevice = _midiAPI_InOpen(0, $hGUI, 0, $CALLBACK_WINDOW)
		Do
		Until GUIGetMsg() = $GUI_EVENT_CLOSE
		_midiAPI_InStop($hDevice)
		_midiAPI_InClose($hDevice)
	Else
		MsgBox(0, "Error", "No devices were found")
	EndIf

	_midiAPI_Shutdown()
	GUIDelete($hGUI)
EndFunc   ;==>Example

Func MidiMsg($hWnd, $iMsg, $wParam, $lParam)
	#forceref $hWnd, $iMsg, $wParam, $lParam

	Local $tCaps, $sDevname
	Switch $iMsg
		Case $MM_MIM_OPEN

			$tCaps = DllStructCreate($tag_midiincaps)
			_midiAPI_InGetDevCaps($wParam, DllStructGetPtr($tCaps))
			$sDevname = DllStructGetData($tCaps, "szPname")
			_midiAPI_InStart($wParam)

			GUICtrlSetData(-1, StringFormat("Monitoring [%s].\r\nGenerate some messages.\r\n\r\n", $sDevname), 1)

		Case $MM_MIM_DATA
			;write midi messages to the edit box
			GUICtrlSetData(-1, BinaryMid($lParam, 1, 3) & @CRLF, 1)
	EndSwitch
EndFunc   ;==>MidiMsg

