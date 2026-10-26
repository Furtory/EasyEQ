#include <midiApi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice

	$hGUI = GUICreate("dummy")
	GUIRegisterMsg($MM_MOM_OPEN, "MidiCallback")
	GUIRegisterMsg($MM_MOM_CLOSE, "MidiCallback")

	_midiAPI_Startup()

	;Open the first device with a window callback mechanism
	$hDevice = _midiAPI_StreamOpen(0, $hGUI, 1, $CALLBACK_WINDOW)
	Sleep(100)

	_midiAPI_StreamClose($hDevice)
	Sleep(100)

	_midiAPI_Shutdown()
	GUIDelete($hGUI)
EndFunc   ;==>Example

Func MidiCallback($hWnd, $iMsg, $wParam, $lParam)
	#forceref $hWnd, $iMsg, $wParam, $lParam

	Switch $iMsg
		Case $MM_MOM_OPEN
			MsgBox(0, "Opened device:", "Handle: " & $wParam)

		Case $MM_MOM_CLOSE
			MsgBox(0, "Closed device:", "Handle: " & $wParam)
	EndSwitch
EndFunc   ;==>MidiCallback
