#include <midiApi.au3>

Example()

Func Example()

	Local $hStream
	Local $iPPQ = 32, $iBPM = 180, $iFPS = 24, $iTPF = 100

	_midiAPI_Startup()
	$hStream = _midiAPI_StreamOpen(0)
	If Not @error Then

		MsgBox(0, "Set Timings", StringFormat("Tempo:\t%d bpm\nTimeDiv:\t%d ppq", $iBPM, $iPPQ))
		SetTimeDivPPQ($hStream, $iPPQ)
		SetTempo($hStream, $iBPM)
		GetTimings($hStream)

		MsgBox(0, "Set Timings", StringFormat("TimeDiv:\t%d fps, %d tpf", $iFPS, $iTPF))
		SetTimeDivFPS($hStream, $iFPS, $iTPF)
		GetTimings($hStream)

		_midiAPI_OutClose($hStream)
	Else
		MsgBox(0, "Error", "Could not open the device.")
	EndIf
	_midiAPI_Shutdown()

EndFunc   ;==>Example

Func SetTimeDivFPS($hStream, $iFPS, $iTPF)
	Local $tTimeDiv, $pTimeDiv, $iTimeDiv
	Local Const $iTimeDivFPS = 0x80000000
	Local Const $iSetTimeDiv = BitOR($MIDIPROP_SET, $MIDIPROP_TIMEDIV)

	;Setup the timediv struct
	$tTimeDiv = DllStructCreate($tag_midiproptimediv)
	DllStructSetData($tTimeDiv, "cbStruct", DllStructGetSize($tTimeDiv))
	$pTimeDiv = DllStructGetPtr($tTimeDiv)

	;create the timediv value
	$iTimeDiv = BitOR($iTimeDivFPS, BitShift($iFPS, -16), $iTPF)

	;Set the property
	DllStructSetData($tTimeDiv, "dwTimeDiv", $iTimeDiv)
	_midiAPI_StreamProperty($hStream, $iSetTimeDiv, $pTimeDiv)
	Return SetError(@error)
EndFunc   ;==>SetTimeDivFPS

Func SetTimeDivPPQ($hStream, $iPPQ)
	Local $tTimeDiv, $pTimeDiv
	Local Const $iSetTimeDiv = BitOR($MIDIPROP_SET, $MIDIPROP_TIMEDIV)

	;Setup the timediv struct
	$tTimeDiv = DllStructCreate($tag_midiproptimediv)
	DllStructSetData($tTimeDiv, "cbStruct", DllStructGetSize($tTimeDiv))
	$pTimeDiv = DllStructGetPtr($tTimeDiv)

	;Set the property
	DllStructSetData($tTimeDiv, "dwTimeDiv", $iPPQ)
	_midiAPI_StreamProperty($hStream, $iSetTimeDiv, $pTimeDiv)
	Return SetError(@error)
EndFunc   ;==>SetTimeDivPPQ

Func SetTempo($hStream, $iBPM)
	Local $iTempo, $tTempo, $pTempo
	Local Const $iSetTempo = BitOR($MIDIPROP_SET, $MIDIPROP_TEMPO)

	;convert to microseconds per crotchet
	$iTempo = 60 * 10 ^ 6 / $iBPM

	;Setup the timediv struct
	$tTempo = DllStructCreate($tag_midiproptempo)
	DllStructSetData($tTempo, "cbStruct", DllStructGetSize($tTempo))
	$pTempo = DllStructGetPtr($tTempo)

	;Set the property
	DllStructSetData($tTempo, "dwTempo", $iTempo)
	_midiAPI_StreamProperty($hStream, $iSetTempo, $pTempo)
	Return SetError(@error)
EndFunc   ;==>SetTempo

Func GetTimings($hStream)
	Local $tTimeDiv, $tTempo, $pTimeDiv, $pTempo
	Local $iTimeDiv, $iTempo, $iTempoBPM, $iFPS, $iTPF
	Local Const $iTimeDivFPS = 0x80000000
	Local Const $iGetTempo = BitOR($MIDIPROP_GET, $MIDIPROP_TEMPO)
	Local Const $iGetTimeDiv = BitOR($MIDIPROP_GET, $MIDIPROP_TIMEDIV)

	;Create the timediv struct
	$tTimeDiv = DllStructCreate($tag_midiproptimediv)
	DllStructSetData($tTimeDiv, "cbStruct", DllStructGetSize($tTimeDiv))
	$pTimeDiv = DllStructGetPtr($tTimeDiv)

	;Create the tempo struct
	$tTempo = DllStructCreate($tag_midiproptempo)
	DllStructSetData($tTempo, "cbStruct", DllStructGetSize($tTempo))
	$pTempo = DllStructGetPtr($tTempo)

	;get the timediv property
	_midiAPI_StreamProperty($hStream, $iGetTimeDiv, $pTimeDiv)
	$iTimeDiv = DllStructGetData($tTimeDiv, "dwTimeDiv")

	If BitAND($iTimeDiv, $iTimeDivFPS) Then
		;Format is FPS
		$iTPF = BitAND(0xFFFF, $iTimeDiv)
		$iFPS = BitAND(0x7FFF, BitShift($iTimeDiv, 16))

		MsgBox(0, "Get Timings", _
				StringFormat("TimeDiv:\tFrames per second\nValue:\t%d fps, %d ticks per frame\n\n", $iFPS, $iTPF) & _
				"Tempo is not valid when format is fps.")
	Else
		;Format is PPQ - Get the tempo
		_midiAPI_StreamProperty($hStream, $iGetTempo, $pTempo)
		$iTempo = DllStructGetData($tTempo, "dwTempo")

		;convert to BPM
		If $iTempo Then $iTempoBPM = 60 * (10 ^ 6 / $iTempo)

		MsgBox(0, "Get Timings", _
				StringFormat("Tempo:\t%d microseconds per crotchet (%d bpm)\n\n", $iTempo, $iTempoBPM) & _
				StringFormat("TimeDiv:\tPulses per quarter note\nValue:\t%d ppq\n\n", $iTimeDiv))
	EndIf
EndFunc   ;==>GetTimings
