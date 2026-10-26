#include-once

; #INDEX# =======================================================================================================================
; Title .........: Midi API Constants
; AutoIt Version : 3.3.14.5
; Description ...: Support for midiAPI.au3
; Author(s) .....: MattyD
; ===============================================================================================================================

; #CONSTANTS# ===================================================================================================================
;Errors
Global Const $MAXERRORLENGTH = 256 ;For midiXGetErrorText

Global Const $MMSYSERR_BASE = 0
Global Const $MIDIERR_BASE = 64
Global Const $MMSYSERR_NOERROR = 0
Global Const $MMSYSERR_ERROR = ($MMSYSERR_BASE + 1)
Global Const $MMSYSERR_BADDEVICEID = ($MMSYSERR_BASE + 2)
Global Const $MMSYSERR_NOTENABLED = ($MMSYSERR_BASE + 3)
Global Const $MMSYSERR_ALLOCATED = ($MMSYSERR_BASE + 4)
Global Const $MMSYSERR_INVALHANDLE = ($MMSYSERR_BASE + 5)
Global Const $MMSYSERR_NODRIVER = ($MMSYSERR_BASE + 6)
Global Const $MMSYSERR_NOMEM = ($MMSYSERR_BASE + 7)
Global Const $MMSYSERR_NOTSUPPORTED = ($MMSYSERR_BASE + 8)
Global Const $MMSYSERR_BADERRNUM = ($MMSYSERR_BASE + 9)
Global Const $MMSYSERR_INVALFLAG = ($MMSYSERR_BASE + 10)
Global Const $MMSYSERR_INVALPARAM = ($MMSYSERR_BASE + 11)
Global Const $MMSYSERR_HANDLEBUSY = ($MMSYSERR_BASE + 12)
Global Const $MMSYSERR_INVALIDALIAS = ($MMSYSERR_BASE + 13)
Global Const $MMSYSERR_BADDB = ($MMSYSERR_BASE + 14)
Global Const $MMSYSERR_KEYNOTFOUND = ($MMSYSERR_BASE + 15)
Global Const $MMSYSERR_READERROR = ($MMSYSERR_BASE + 16)
Global Const $MMSYSERR_WRITEERROR = ($MMSYSERR_BASE + 17)
Global Const $MMSYSERR_DELETEERROR = ($MMSYSERR_BASE + 18)
Global Const $MMSYSERR_VALNOTFOUND = ($MMSYSERR_BASE + 19)
Global Const $MMSYSERR_NODRIVERCB = ($MMSYSERR_BASE + 20)
Global Const $MMSYSERR_LASTERROR = ($MMSYSERR_BASE + 20)
Global Const $MIDIERR_UNPREPARED = $MIDIERR_BASE
Global Const $MIDIERR_STILLPLAYING = ($MIDIERR_BASE + 1)
Global Const $MIDIERR_NOMAP = ($MIDIERR_BASE + 2)
Global Const $MIDIERR_NOTREADY = ($MIDIERR_BASE + 3)
Global Const $MIDIERR_NODEVICE = ($MIDIERR_BASE + 4)
Global Const $MIDIERR_INVALIDSETUP = ($MIDIERR_BASE + 5)
Global Const $MIDIERR_BADOPENMODE = ($MIDIERR_BASE + 6)
Global Const $MIDIERR_DONT_CONTINUE = ($MIDIERR_BASE + 7)
Global Const $MIDIERR_LASTERROR = ($MIDIERR_BASE + 7)

Global Const $MIDISTRM_ERROR = -2

;Use with midiXGetDevCaps
Global Const $MAXPNAMELEN = 32

Global Const $MIDICAPS_VOLUME = 1
Global Const $MIDICAPS_LRVOLUME = 2
Global Const $MIDICAPS_CACHE = 4
Global Const $MIDICAPS_STREAM = 8

Global Const $MOD_MIDIPORT = 1 ;/* output port */
Global Const $MOD_SYNTH = 2 ;/* generic internal synth */
Global Const $MOD_SQSYNTH = 3 ;/* square wave internal synth */
Global Const $MOD_FMSYNTH = 4 ;/* FM internal synth */
Global Const $MOD_MAPPER = 5 ;/* MIDI mapper */
Global Const $MOD_WAVETABLE = 6 ;/* hardware wavetable synth */
Global Const $MOD_SWSYNTH = 7 ;/* software synth */

;Use with midiXOpen
Global Const $MIDIMAPPER = -1 ;Use as device ID to get default device
Global Const $MIDI_MAPPER = -1

Global Const $CALLBACK_TYPEMASK = 0x00070000 ;/* callback type mask */
Global Const $CALLBACK_NULL = 0x00000000 ;/* no callback */
Global Const $CALLBACK_WINDOW = 0x00010000 ;/* dwCallback is a HWND */
Global Const $CALLBACK_TASK = 0x00020000 ;/* dwCallback is a HTASK */
Global Const $CALLBACK_FUNCTION = 0x00030000 ;/* dwCallback is a FARPROC */
Global Const $CALLBACK_THREAD = ($CALLBACK_TASK) ;/* thread ID replaces 16 bit task */
Global Const $CALLBACK_EVENT = 0x00050000 ;/* dwCallback is an EVENT Handle */
Global Const $MIDI_IO_STATUS = 0x00000020

;Window/thread callback messages
Global Const $MM_MIM_OPEN = 0x3C1
Global Const $MM_MIM_CLOSE = 0x3C2
Global Const $MM_MIM_DATA = 0x3C3
Global Const $MM_MIM_LONGDATA = 0x3C4
Global Const $MM_MIM_ERROR = 0x3C5
Global Const $MM_MIM_LONGERROR = 0x3C6
Global Const $MM_MOM_OPEN = 0x3C7
Global Const $MM_MOM_CLOSE = 0x3C8
Global Const $MM_MOM_DONE = 0x3C9
Global Const $MM_MOM_POSITIONCB = 0x3CA
Global Const $MM_MIM_MOREDATA = 0x3CC

;Callback function messages
Global Const $MIM_OPEN = $MM_MIM_OPEN
Global Const $MIM_CLOSE = $MM_MIM_CLOSE
Global Const $MIM_DATA = $MM_MIM_DATA
Global Const $MIM_LONGDATA = $MM_MIM_LONGDATA
Global Const $MIM_ERROR = $MM_MIM_ERROR
Global Const $MIM_LONGERROR = $MM_MIM_LONGERROR
Global Const $MOM_OPEN = $MM_MOM_OPEN
Global Const $MOM_CLOSE = $MM_MOM_CLOSE
Global Const $MOM_DONE = $MM_MOM_DONE
Global Const $MIM_MOREDATA = $MM_MIM_MOREDATA
Global Const $MOM_POSITIONCB = $MM_MOM_POSITIONCB

;Stream playback
Global Const $MEVT_F_SHORT = 0
Global Const $MEVT_F_LONG = 0x80000000
Global Const $MEVT_F_CALLBACK = 0x40000000
Global Const $MEVT_SHORTMSG = 0
Global Const $MEVT_TEMPO = 1
Global Const $MEVT_NOP = 2
Global Const $MEVT_LONGMSG = 0x80
Global Const $MEVT_COMMENT = 0x82
Global Const $MEVT_VERSION = 0x84

;Use with midiStreamPosition
Global Const $TIME_MS = 1
Global Const $TIME_SAMPLES = 2
Global Const $TIME_BYTES = 4
Global Const $TIME_SMPTE = 8
Global Const $TIME_MIDI = 16
Global Const $TIME_TICKS = 32

;Use with midiStreamPorperty
Global Const $MIDIPROP_SET = 0x80000000
Global Const $MIDIPROP_GET = 0x40000000
Global Const $MIDIPROP_TIMEDIV = 1
Global Const $MIDIPROP_TEMPO = 2

;Patch caching
Global Const $MIDIPATCHSIZE = 128

Global Const $MIDI_CACHE_ALL = 1
Global Const $MIDI_CACHE_BESTFIT = 2
Global Const $MIDI_CACHE_QUERY = 3
Global Const $MIDI_UNCACHE = 4

;for midiheader struct - buffer status
Global Const $MHDR_DONE = 1
Global Const $MHDR_PREPARED = 2
Global Const $MHDR_INQUEUE = 4
Global Const $MHDR_ISSTRM = 8
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;$tag_keyarray
;$tag_midievent
;$tag_midihdr
;$tag_midiincaps
;$tag_midioutcaps
;$tag_midiproptempo
;$tag_midiproptimediv
;$tag_midistrmbuffver
;$tag_mmtime
;$tag_mmtime_smpte
;$tag_patcharray
; ===============================================================================================================================

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_keyarray
; Description ...: Used for caching and uncaching percussion patches for a software synthesizer
; Fields ........: KEYARRAY   - Each element corresponds to a key-based percussion patch.
;                               Each bit of an element represents one MIDI channel (Least significant bit is channel 0)
; Author ........: MattyD
; Remarks .......: For use with _midiOutCacheDrumPatches
; Related .......: _midiOutCacheDrumPatches
; ===============================================================================================================================
Global Const $tag_keyarray = StringFormat("word KEYARRAY[%d]", $MIDIPATCHSIZE)

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midievent
; Description ...: Describes a MIDI event in a stream buffer
; Fields ........: dwDeltaTime - Time, in MIDI ticks, between the previous event and the current event.
;                  dwStreamID  - Reserved.
;                  dwEvent     - High-order byte:   Combined event code ($MEVT_*) and flags ($MEVT_F_*)
;                              - Remaining 24 bits: Event parameters (short msg) or length (long msg)
; Author ........: MattyD
; Remarks .......: The length of a tick is defined by the time format and possibly the tempo associated with the stream.
;                  The high byte of dwEvent contains flags and an event code.
;                  Either the MEVT_F_LONG or MEVT_F_SHORT flag must be specified. The MEVT_F_CALLBACK flag is optional.
;                  If dwEvent specifies MEVT_F_LONG and the length of a buffer, an array of DWORDs must to be appended to this
;                  struct to accommodate the buffer. The data in this buffer must be padded with zeros so that an integral
;                  number of DWORD values are stored.
; Related .......: $tag_midistrmbuffver
; ===============================================================================================================================
Global Const $tag_midievent = _
		"dword dwDeltaTime;" & _
		"dword dwStreamID;" & _
		"dword dwEvent;"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midihdr
; Description ...: Midi buffer header
; Fields ........: lpData            - Pointer to MIDI data
;                  dwBufferLength    - Size of the buffer.
;                  dwBytesRecorded   - Actual amount of data in the buffer.
;                  dwUser            - Custom user data.
;                  dwFlags           - Buffer state flags ($MHDR_* flags)
;                  lpNext            - Reserved.
;                  reserved          - Reserved.
;                  dwOffset          - Offset into the buffer when a callback is performed.
;                  dwReserved        - Reserved.
; Author ........: MattyD
; Remarks .......: Used for passing midi data between an application and midi devices or streams
; Related .......: _midiInAddBuffer, _MidiOutLongMsg, _MidiStreamOut, _MidiInPrepareBuffer, _MidiInUnprepareBuffer,
;                  _MidiOutPrepareBuffer, _MidiOutUnprepareBuffer, _MidiInReset, _MidiOutReset, _midiStreamStop
; ===============================================================================================================================
Global Const $tag_midihdr = _
		"ptr lpData;" & _
		"dword dwBufferLength;" & _
		"dword dwBytesRecorded;" & _
		"dword_ptr dwUser;" & _
		"dword dwFlags;" & _
		"ptr lpNext;" & _
		"dword_ptr reserved;" & _
		"dword dwOffset;" & _
		"dword_ptr dwReserved[8];"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midiincaps
; Description ...: Midi In Device Capabilities
; Fields ........: wMid            - Manufacturer ID
;                  wPid            - Product ID
;                  vDriverVersion  - High-order byte: major version
;                                  - Low-order byte: minor version
;                  szPname         - Product name
;                  dwSupport       - Reserved
; Author ........: MattyD
; Remarks .......: For use with _midiInGetDevCaps. Cannot be used with the ANSI version of the function.
; Related .......: _midiInGetDevCaps, $tag_midioutcaps
; ===============================================================================================================================
Global Const $tag_midiincaps = _
		"word wMid;" & _
		"word wPid;" & _
		"uint vDriverVersion;" & _
		"wchar szPname[" & $MAXPNAMELEN & "];" & _
		"dword dwSupport;"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midioutcaps
; Description ...: Midi out device capabilities
; Fields ........: wMid            - Manufacturer ID
;                  wPid            - Product ID
;                  vDriverVersion  - High-order byte: major version
;                                  - Low-order byte: minor version
;                  szPname         - Product name
;                  wTechnology     - MIDI output device type. ($MOD_* Enumeration)
;                  wVoices         - Number of voices (internal synth only)
;                  wNotes          - Max number of notes (internal synth only)
;                  wChannelMask    - Channels used (internal synth only)
;                  dwSupport       - Functionality supported by the device. ($MIDICAPS_* flags)
; Author ........: MattyD
; Remarks .......: For use with _midiInGetDevCaps. Cannot be used with the ANSI version of the function.
; Related .......: _midiOutGetDevCaps, $tag_midioutcaps
; ===============================================================================================================================
Global Const $tag_midioutcaps = _
		"word wMid;" & _
		"word wPid;" & _
		"uint vDriverVersion;" & _
		"wchar szPname[" & $MAXPNAMELEN & "];" & _
		"word wTechnology;" & _
		"word wVoices;" & _
		"word wNotes;" & _
		"word wChannelMask;" & _
		"dword dwSupport;"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midiproptimediv
; Description ...: Used for setting and retreiving the tempo property for a midi stream
; Fields ........: cbStruct  - Size of the struct
;                  dwTempo   - Tempo in microseconds per quarter note
; Author ........: MattyD
; Remarks .......: For use with _midiStreamProperty
; Related .......: _midiStreamProperty
; ===============================================================================================================================
Global Const $tag_midiproptempo = "dword cbStruct;dword dwTempo;"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midiproptimediv
; Description ...: Used for setting and retreiving the time division property for a midi stream
; Fields ........: cbStruct  - Size of the struct
;                  dwTimeDiv - Time division as defined in the MIDI 1.0 specification
; Author ........: MattyD
; Remarks .......: For use with _midiStreamProperty
; Related .......: _midiStreamProperty
; ===============================================================================================================================
Global Const $tag_midiproptimediv = "dword cbStruct;dword dwTimeDiv;"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_midistrmbuffver
; Description ...: Contains version information for a $MEVT_VERSION event in a stream buffer
; Fields ........: dwVersion    - High-order byte: major version
;                               - Low-order byte: minor version
;                  dwMid        - Manufacturer identifier.
;                  dwOEMVersion - OEM version of the stream
; Author ........: MattyD
; Remarks .......: Append this structure to $tag_midievent for $MEVT_VERSION events
; Related .......: $tag_midievent
; ===============================================================================================================================
Global Const $tag_midistrmbuffver = _
		"dword dwVersion;" & _
		"dword dwMid;" & _
		"dword dwOEMVersion;"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_mmtime
; Description ...: Generic multimedia time struct
; Fields ........: wType   - Time format ($TIME_* enumeration)
;                  data    - Time data.
; Author ........: MattyD
; Remarks .......: For use with _midiStreamPosition.
;                  The data element is not large enough for SMPTE data. Use $tag_mmtime_smpte for this.
;                  The following indicates what the data element represents for each wType.
;                  $TIME_BYTES   - Byte offset from beginning of the file.
;                  $TIME_MIDI    - Song pointer position
;                  $TIME_MS      - Time in milliseconds.
;                  $TIME_SAMPLES - Number of waveform-audio samples.
;                  $TIME_TICKS   - Ticks within a MIDI stream.
; Related .......: $tag_mmtime_smpte, _midiStreamPosition
; ===============================================================================================================================
Global Const $tag_mmtime = "uint wType;dword dwData"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_mmtime_smpte
; Description ...: Multimedia time struct (SMPTE)
; Fields ........: wType    - set to $TIME_SMPTE
;                  hour     - Hours
;                  min      - Minutes
;                  sec      - Seconds
;                  frame    - Frames per second (24, 25, 29 (30 drop), or 30).
;                  dummy    - Dummy byte for alignment
;                  pad      - Padding
; Author ........: MattyD
; Remarks .......: For use with _midiStreamPosition
; Related .......: _midiStreamPosition
; ===============================================================================================================================
Global Const $tag_mmtime_smpte = _
		"uint wType;" & _
		"byte hour;" & _
		"byte min;" & _
		"byte sec;" & _
		"byte frame;" & _
		"byte fps;" & _
		"byte dummy;" & _
		"byte pad[2];"

; #STRUCTURE# ===================================================================================================================
; Name...........: $tag_patcharray
; Description ...: Used for caching and uncaching patches for a software synthesizer
; Fields ........: PATCHARRAY - Each element corresponds to a patch.
;                               Each bit of an element represents one MIDI channel (Least significant bit is channel 0)
; Author ........: MattyD
; Remarks .......: For use with _midiOutCachePatches
; Related .......: _midiOutCachePatches
; ===============================================================================================================================
Global Const $tag_patcharray = StringFormat("word PATCHARRAY[%d]", $MIDIPATCHSIZE)
