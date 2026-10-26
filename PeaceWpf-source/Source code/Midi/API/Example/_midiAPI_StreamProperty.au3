#include <midiApi.au3>

Example()

Func Example()

	Local $hStream, $tTempo, $iTempo
	Local Const $iGetTempo = BitOR($MIDIPROP_GET, $MIDIPROP_TEMPO)

	_midiAPI_Startup()
	$hStream = _midiAPI_StreamOpen(0)

	If Not @error Then
		$tTempo = DllStructCreate($tag_midiproptempo)
		DllStructSetData($tTempo, "cbStruct", DllStructGetSize($tTempo))
		_midiAPI_StreamProperty($hStream, $iGetTempo, DllStructGetPtr($tTempo))

		$iTempo = DllStructGetData($tTempo, "dwTempo")
		MsgBox(0, "Get Property", StringFormat("Tempo is %d microseconds per crotchet", $iTempo))

		_midiAPI_OutClose($hStream)
	Else
		MsgBox(0, "Error", "Could not open the device.")
	EndIf

	_midiAPI_Shutdown()

EndFunc   ;==>Example
