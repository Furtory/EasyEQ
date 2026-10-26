#include <midi.au3>

Example()

Func Example()
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $hGUI, $hDevice, $iPianoChan = 1, $iOrganChan = 2, $iVelocity

	_midi_Startup()
	$hGUI = GUICreate("Dummy")
	$hDevice = _Midi_OpenOutput(0, $hGUI)

	_midi_SendProgramChange($hDevice, $iPianoChan, $PGM_GRAND_PIANO)
	_midi_SendProgramChange($hDevice, $iOrganChan, $PGM_DBAR_ORGAN)

	$iVelocity = 0x60
	For $i = 0 To UBound($aiC7) - 1 Step 2
		_midi_SendNoteOn($hDevice, $iPianoChan, $aiC7[$i] - 12, $iVelocity)
	Next

	$iVelocity = 0x40
	For $i = 1 To UBound($aiC7) - 1
		Sleep(200)
		_midi_SendNoteOn($hDevice, $iOrganChan, $aiC7[$i], $iVelocity)
	Next
	Sleep(500)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iPianoChan, $aiC7[$i] - 12)
		_midi_SendNoteOff($hDevice, $iOrganChan, $aiC7[$i])
	Next
	Sleep(500)

	_midi_SendProgramChange($hDevice, $iOrganChan, $PGM_GRAND_PIANO)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

