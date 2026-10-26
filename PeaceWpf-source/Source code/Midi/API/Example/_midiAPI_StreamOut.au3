#include <midiApi.au3>

Example()

Func Example()
	Local $hStream, $hCallBack, $pMidiHdr

	; 24 Ticks per quarter note.
	; 1 quarter note = 500000 microseconds (120bpm)
	Local $iPPQ = 24, $iTempo = 500000

	;$aMessages[Short Message][Delta in ticks]
	Local $aMessages[7][2] = [ _
			[Binary("0x903C50"), 0], _
			[Binary("0x913F50"), 24], _
			[Binary("0xE17F7F"), 12], _
			[Binary("0x904D50"), 12], _
			[Binary("0x803C00"), 24], _
			[Binary("0x804D00"), 0], _
			[Binary("0x813F00"), 12]]

	_midiAPI_Startup()

	;Open stream & register callback.
	$hCallBack = DllCallbackRegister("MidiCallback", "none", "hwnd;uint;dword_ptr;dword_ptr;dword_ptr")
	$hStream = _midiAPI_StreamOpen(0, DllCallbackGetPtr($hCallBack), 0, $CALLBACK_FUNCTION)

	If Not @error Then
		;set the tempo
		SetTiming($hStream, $iPPQ, $iTempo)

		;queue events
		For $i = 0 To UBound($aMessages) - 1
			$pMidiHdr = ShortEvtQueue($aMessages[$i][0], $aMessages[$i][1])
		Next

		;prepare header and send.
		_midiAPI_OutPrepareHeader($hStream, $pMidiHdr)
		_midiAPI_StreamOut($hStream, $pMidiHdr)

		;start the stream & wait for synth.
		_midiAPI_StreamRestart($hStream)
		Sleep(2000)

		_midiAPI_StreamClose($hStream)

	Else
		MsgBox(0, "Error", "The device failed to open.")
	EndIf

	_midiAPI_Shutdown()
	DllCallbackFree($hCallBack)
EndFunc   ;==>Example


Func SetTiming($hStream, $iPPQ, $iTempo)
	Local $tStrmProp, $pProp

	;ppq = Pulses per quarter note.
	;Ignore first bit (which would indicate the timeDiv type is SMPTE, not PPQ)
	$iPPQ = BitAND($iPPQ, 0x7FFF)

	;set the timediv property
	$tStrmProp = DllStructCreate($tag_midiproptimediv)
	DllStructSetData($tStrmProp, "cbStruct", DllStructGetSize($tStrmProp)) ;8 Bytes
	DllStructSetData($tStrmProp, "dwTimeDiv", $iPPQ)
	$pProp = DllStructGetPtr($tStrmProp)

	_midiAPI_StreamProperty($hStream, BitOR($MIDIPROP_SET, $MIDIPROP_TIMEDIV), $pProp)

	;set the tempo property
	$tStrmProp = DllStructCreate($tag_midiproptempo)
	DllStructSetData($tStrmProp, "cbStruct", DllStructGetSize($tStrmProp)) ;8 Bytes
	DllStructSetData($tStrmProp, "dwTempo", $iTempo)
	$pProp = DllStructGetPtr($tStrmProp)

	_midiAPI_StreamProperty($hStream, BitOR($MIDIPROP_SET, $MIDIPROP_TEMPO), $pProp)

EndFunc   ;==>SetTiming

Func ShortEvtQueue($vParam, $iDelta = 0)

	Local Static $pMidiHdr, $tMidiHdr, $tBuffer
	Local Static $pBuffer, $pEvent
	Local $tEvent

	;If no buffer is setup yet...
	If Not $pMidiHdr Then
		;create the buffer
		$tBuffer = DllStructCreate("byte[256]")
		$pBuffer = DllStructGetPtr($tBuffer)

		;Create the header
		$tMidiHdr = DllStructCreate($tag_midihdr)
		$pMidiHdr = DllStructGetPtr($tMidiHdr)
		DllStructSetData($tMidiHdr, "lpData", $pBuffer)
		DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))

		;set the pointer for the first event.
		$pEvent = $pBuffer
	EndIf

	;Create event and add to buffer.
	If IsBinary($vParam) Then

		;make sure this is a short message.
		If BinaryLen($vParam) <> 3 Then Return SetError(1)

		;create the event.
		$tEvent = DllStructCreate($tag_midievent, $pEvent)
		DllStructSetData($tEvent, "dwDeltaTime", $iDelta)
		DllStructSetData($tEvent, "dwEvent", BitOR($MEVT_F_SHORT, $vParam))

		;set the pointer for the next event.
		$pEvent += DllStructGetSize($tEvent)

		;update the cumulative size of midi events in the header
		DllStructSetData($tMidiHdr, "dwBytesRecorded", $pEvent - $pBuffer)

		Return $pMidiHdr

		;The driver is done with a buffer, so we want to free it.
	ElseIf IsPtr($vParam) Then
		$pMidiHdr = 0
		$tMidiHdr = 0
		$tBuffer = 0
		$pBuffer = 0
		$pEvent = 0
	EndIf

EndFunc   ;==>ShortEvtQueue

Func MidiCallback($hDevice, $iMsg, $inst, $iParam1, $iParam2)
	#forceref $hDevice, $iMsg, $inst, $iParam1, $iParam2

	;Buffer has been returned from driver.
	If $iMsg = $MOM_DONE Then

		;We should cleanup. Param1 is a pMidiHdr when receiving a $MOM_DONE message.
		_midiAPI_OutUnprepareHeader($hDevice, $iParam1)

		;Free the buffer
		ShortEvtQueue(Ptr($iParam1))
	EndIf
EndFunc   ;==>MidiCallback
