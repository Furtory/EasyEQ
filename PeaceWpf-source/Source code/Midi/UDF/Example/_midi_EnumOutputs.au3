#AutoIt3Wrapper_Au3Check_Parameters=-q -d -w 1 -w 2 -w 3 -w 4 -w 5 -w 6 -w 7
#include <midi.au3>

Example()

Func Example()

	Local $asDevices, $sMessage

	_midi_Startup()

	$asDevices = _midi_EnumOutputs()
	If @error Then $sMessage = "No output devices were found."

	For $i = 0 To UBound($asDevices) - 1
		$sMessage &= StringFormat('ID: [%d]\tNAME: [%s]\n', $i, $asDevices[$i])
	Next
	MsgBox(0, "Found Devices:", $sMessage)

	_midi_Shutdown()
EndFunc