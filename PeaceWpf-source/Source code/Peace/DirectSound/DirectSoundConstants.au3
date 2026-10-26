#include-once
;#include "FASM.au3"

; ###############################################################################################################################
; # DirectSound Error Codes
; ###############################################################################################################################
Global Const $DSERR_NOHWND  = 0x80070006 ;Handle that is not valid
Global Const $DSERR_NOPTR   = 0x80004003 ;Pointer that is not valid
Global Const $DSERR_NOOBJ   = 0x800710D8 ;The object identifier does not represent a valid object
Global Const $DSERR_PARAM   = 0x80070057 ;One or more arguments are not valid
Global Const $DSERR_UFAIL   = 0x8000FFFF ;Unexpected failure
Global Const $DSERR_OBJFAIL = 0x80080001 ;Attempt to create a class object failed.

;Global Const $DSERR_STRM    = 0x88982f72 ;Failed to read from the stream.
;Global Const $DSERR_BADHDR  = 0x88982f61 ;The header is unrecognized.
;Global Const $DSERR_FRMT    = 0x83750001 ;Unsupported format.
;Global Const $DSERR_FILE    = 0x80092003 ;An error occurred while reading or writing to a file.
;Global Const $DSERR_INPUT   = 0x803D0000 ;The input data was not in the expected format or did not have the expected value.

Global Const $DS_OK                 = 0x00000000
Global Const $DS_NO_VIRTUALIZATION  = 0x08780000

Global Const $DSERR_ALLOCATED           = 0x8878000A
Global Const $DSERR_CONTROLUNAVAIL      = 0x8878001E
Global Const $DSERR_INVALIDPARAM        = 0x80070057
Global Const $DSERR_INVALIDCALL         = 0x88780032
Global Const $DSERR_GENERIC             = 0x80004005
Global Const $DSERR_PRIOLEVELNEEDED     = 0x88780046
Global Const $DSERR_OUTOFMEMORY         = 0x8007000E
Global Const $DSERR_BADFORMAT           = 0x88780064
Global Const $DSERR_UNSUPPORTED         = 0x80004001
Global Const $DSERR_NODRIVER            = 0x88780078
Global Const $DSERR_ALREADYINITIALIZED  = 0x88780082
Global Const $DSERR_NOAGGREGATION       = 0x80040110
Global Const $DSERR_BUFFERLOST          = 0x88780096
Global Const $DSERR_OTHERAPPHASPRIO     = 0x887800A0
Global Const $DSERR_UNINITIALIZED       = 0x887800AA
Global Const $DSERR_NOINTERFACE         = 0x80004002
Global Const $DSERR_ACCESSDENIED        = 0x80070005
Global Const $DSERR_BUFFERTOOSMALL      = 0x887800B4
Global Const $DSERR_DS8_REQUIRED        = 0x887800BE
Global Const $DSERR_SENDLOOP            = 0x887800C8
Global Const $DSERR_BADSENDBUFFERGUID   = 0x887800D2
Global Const $DSERR_OBJECTNOTFOUND      = 0x88781161
Global Const $DSERR_FXUNAVAILABLE       = 0x887800DC

Global Const $WAVE_FORMAT_UNKNOWN         = 0x0000 ;Unknown or invalid format tag
Global Const $WAVE_FORMAT_PCM             = 0x0001 ;Pulse Code Modulation
Global Const $WAVE_FORMAT_ADPCM           = 0x0002 ;Microsoft Adaptive Differental PCM
Global Const $WAVE_FORMAT_IEEE_FLOAT      = 0x0003 ;32-bit floating-point
Global Const $WAVE_FORMAT_ALAW            = 0x0006
Global Const $WAVE_FORMAT_MPEG            = 0x0050
Global Const $WAVE_FORMAT_MPEGLAYER3      = 0x0055 ;ISO/MPEG Layer3
Global Const $WAVE_FORMAT_DOLBY_AC3_SPDIF = 0x0092 ;Dolby Audio Codec 3 over S/PDIF
Global Const $WAVE_FORMAT_WMAUDIO2        = 0x0161 ;Windows Media Audio
Global Const $WAVE_FORMAT_WMAUDIO3        = 0x0162 ;Windows Media Audio Pro
Global Const $WAVE_FORMAT_WMASPDIF        = 0x0164 ;Windows Media Audio over S/PDIF
Global Const $WAVE_FORMAT_EXTENSIBLE      = 0xFFFE ;All WAVEFORMATEXTENSIBLE formats

Global Const $KSDATAFORMAT_SUBTYPE_PCM        = "{00000001-0000-0010-8000-00AA00389B71}"
Global Const $KSDATAFORMAT_SUBTYPE_ADPCM      = "{00000002-0000-0010-8000-00AA00389B71}"
Global Const $KSDATAFORMAT_SUBTYPE_IEEE_FLOAT = "{00000003-0000-0010-8000-00AA00389B71}"

Global Const $WAVE_INVALIDFORMAT   = 0x00000000 ;invalid format
Global Const $WAVE_FORMAT_1M08     = 0x00000001 ;11.025 kHz, Mono,   8-bit
Global Const $WAVE_FORMAT_1S08     = 0x00000002 ;11.025 kHz, Stereo, 8-bit
Global Const $WAVE_FORMAT_1M16     = 0x00000004 ;11.025 kHz, Mono,   16-bit
Global Const $WAVE_FORMAT_1S16     = 0x00000008 ;11.025 kHz, Stereo, 16-bit
Global Const $WAVE_FORMAT_2M08     = 0x00000010 ;22.05  kHz, Mono,   8-bit
Global Const $WAVE_FORMAT_2S08     = 0x00000020 ;22.05  kHz, Stereo, 8-bit
Global Const $WAVE_FORMAT_2M16     = 0x00000040 ;22.05  kHz, Mono,   16-bit
Global Const $WAVE_FORMAT_2S16     = 0x00000080 ;22.05  kHz, Stereo, 16-bit
Global Const $WAVE_FORMAT_4M08     = 0x00000100 ;44.1   kHz, Mono,   8-bit
Global Const $WAVE_FORMAT_4S08     = 0x00000200 ;44.1   kHz, Stereo, 8-bit
Global Const $WAVE_FORMAT_4M16     = 0x00000400 ;44.1   kHz, Mono,   16-bit
Global Const $WAVE_FORMAT_4S16     = 0x00000800 ;44.1   kHz, Stereo, 16-bit
Global Const $WAVE_FORMAT_48M08    = 0x00001000 ;48     kHz, Mono,   8-bit
Global Const $WAVE_FORMAT_48S08    = 0x00002000 ;48     kHz, Stereo, 8-bit
Global Const $WAVE_FORMAT_48M16    = 0x00004000 ;48     kHz, Mono,   16-bit
Global Const $WAVE_FORMAT_48S16    = 0x00008000 ;48     kHz, Stereo, 16-bit
Global Const $WAVE_FORMAT_96M08    = 0x00010000 ;96     kHz, Mono,   8-bit
Global Const $WAVE_FORMAT_96S08    = 0x00020000 ;96     kHz, Stereo, 8-bit
Global Const $WAVE_FORMAT_96M16    = 0x00040000 ;96     kHz, Mono,   16-bit
Global Const $WAVE_FORMAT_96S16    = 0x00080000 ;96     kHz, Stereo, 16-bit

Global Const $DSFX_LOCHARDWARE    = 0x00000001
Global Const $DSFX_LOCSOFTWARE    = 0x00000002
Global Const $DSCFX_LOCHARDWARE   = 0x00000001
Global Const $DSCFX_LOCSOFTWARE   = 0x00000002
Global Const $DSCFXR_LOCHARDWARE  = 0x00000010
Global Const $DSCFXR_LOCSOFTWARE  = 0x00000020

Global Const $KSPROPERTY_SUPPORT_GET  = 0x00000001
Global Const $KSPROPERTY_SUPPORT_SET  = 0x00000002

Global Const $DSFXGARGLE_WAVE_TRIANGLE  = 0
Global Const $DSFXGARGLE_WAVE_SQUARE    = 1
Global Const $DSFXGARGLE_RATEHZ_MIN     = 1
Global Const $DSFXGARGLE_RATEHZ_MAX     = 1000

Global Const $DSFXCHORUS_WAVE_TRIANGLE  =  0
Global Const $DSFXCHORUS_WAVE_SIN       =  1
Global Const $DSFXCHORUS_WETDRYMIX_MIN  =  0.0
Global Const $DSFXCHORUS_WETDRYMIX_MAX  =  100.0
Global Const $DSFXCHORUS_DEPTH_MIN      =  0.0
Global Const $DSFXCHORUS_DEPTH_MAX      =  100.0
Global Const $DSFXCHORUS_FEEDBACK_MIN   = -99.0
Global Const $DSFXCHORUS_FEEDBACK_MAX   =  99.0
Global Const $DSFXCHORUS_FREQUENCY_MIN  =  0.0
Global Const $DSFXCHORUS_FREQUENCY_MAX  =  10.0
Global Const $DSFXCHORUS_DELAY_MIN      =  0.0
Global Const $DSFXCHORUS_DELAY_MAX      =  20.0
Global Const $DSFXCHORUS_PHASE_MIN      =  0
Global Const $DSFXCHORUS_PHASE_MAX      =  4
Global Const $DSFXCHORUS_PHASE_NEG_180  =  0
Global Const $DSFXCHORUS_PHASE_NEG_90   =  1
Global Const $DSFXCHORUS_PHASE_ZERO     =  2
Global Const $DSFXCHORUS_PHASE_90       =  3
Global Const $DSFXCHORUS_PHASE_180      =  4

Global Const $DSFXFLANGER_WAVE_TRIANGLE  =  0
Global Const $DSFXFLANGER_WAVE_SIN       =  1
Global Const $DSFXFLANGER_WETDRYMIX_MIN  =  0.0
Global Const $DSFXFLANGER_WETDRYMIX_MAX  =  100.0
Global Const $DSFXFLANGER_FREQUENCY_MIN  =  0.0
Global Const $DSFXFLANGER_FREQUENCY_MAX  =  10.0
Global Const $DSFXFLANGER_DEPTH_MIN      =  0.0
Global Const $DSFXFLANGER_DEPTH_MAX      =  100.0
Global Const $DSFXFLANGER_PHASE_MIN      =  0
Global Const $DSFXFLANGER_PHASE_MAX      =  4
Global Const $DSFXFLANGER_FEEDBACK_MIN   = -99.0
Global Const $DSFXFLANGER_FEEDBACK_MAX   =  99.0
Global Const $DSFXFLANGER_DELAY_MIN      =  0.0
Global Const $DSFXFLANGER_DELAY_MAX      =  4.0
Global Const $DSFXFLANGER_PHASE_NEG_180  =  0
Global Const $DSFXFLANGER_PHASE_NEG_90   =  1
Global Const $DSFXFLANGER_PHASE_ZERO     =  2
Global Const $DSFXFLANGER_PHASE_90       =  3
Global Const $DSFXFLANGER_PHASE_180      =  4

Global Const $DSFXECHO_WETDRYMIX_MIN   = 0.0
Global Const $DSFXECHO_WETDRYMIX_MAX   = 100.0
Global Const $DSFXECHO_FEEDBACK_MIN    = 0.0
Global Const $DSFXECHO_FEEDBACK_MAX    = 100.0
Global Const $DSFXECHO_LEFTDELAY_MIN   = 1.0
Global Const $DSFXECHO_LEFTDELAY_MAX   = 2000.0
Global Const $DSFXECHO_RIGHTDELAY_MIN  = 1.0
Global Const $DSFXECHO_RIGHTDELAY_MAX  = 2000.0
Global Const $DSFXECHO_PANDELAY_MIN    = 0
Global Const $DSFXECHO_PANDELAY_MAX    = 1

Global Const $DSFXDISTORTION_GAIN_MIN                   = -60.0
Global Const $DSFXDISTORTION_GAIN_MAX                   =  0.0
Global Const $DSFXDISTORTION_EDGE_MIN                   =  0.0
Global Const $DSFXDISTORTION_EDGE_MAX                   =  100.0
Global Const $DSFXDISTORTION_POSTEQCENTERFREQUENCY_MIN  =  100.0
Global Const $DSFXDISTORTION_POSTEQCENTERFREQUENCY_MAX  =  8000.0
Global Const $DSFXDISTORTION_POSTEQBANDWIDTH_MIN        =  100.0
Global Const $DSFXDISTORTION_POSTEQBANDWIDTH_MAX        =  8000.0
Global Const $DSFXDISTORTION_PRELOWPASSCUTOFF_MIN       =  100.0
Global Const $DSFXDISTORTION_PRELOWPASSCUTOFF_MAX       =  8000.0

Global Const $DSFXCOMPRESSOR_GAIN_MIN       = -60.0
Global Const $DSFXCOMPRESSOR_GAIN_MAX       =  60.0
Global Const $DSFXCOMPRESSOR_ATTACK_MIN     =  0.01
Global Const $DSFXCOMPRESSOR_ATTACK_MAX     =  500.0
Global Const $DSFXCOMPRESSOR_RELEASE_MIN    =  50.0
Global Const $DSFXCOMPRESSOR_RELEASE_MAX    =  3000.0
Global Const $DSFXCOMPRESSOR_THRESHOLD_MIN  = -60.0
Global Const $DSFXCOMPRESSOR_THRESHOLD_MAX  =  0.0
Global Const $DSFXCOMPRESSOR_RATIO_MIN      =  1.0
Global Const $DSFXCOMPRESSOR_RATIO_MAX      =  100.0
Global Const $DSFXCOMPRESSOR_PREDELAY_MIN   =  0.0
Global Const $DSFXCOMPRESSOR_PREDELAY_MAX   =  4.0

Global Const $DSFXPARAMEQ_CENTER_MIN     =  80.0
Global Const $DSFXPARAMEQ_CENTER_MAX     =  16000.0
Global Const $DSFXPARAMEQ_BANDWIDTH_MIN  =  1.0
Global Const $DSFXPARAMEQ_BANDWIDTH_MAX  =  36.0
Global Const $DSFXPARAMEQ_GAIN_MIN       = -15.0
Global Const $DSFXPARAMEQ_GAIN_MAX       =  15.0

Global Const $DSFX_I3DL2REVERB_ROOM_MIN                   = -10000
Global Const $DSFX_I3DL2REVERB_ROOM_MAX                   =  0
Global Const $DSFX_I3DL2REVERB_ROOM_DEFAULT               = -1000
Global Const $DSFX_I3DL2REVERB_ROOMHF_MIN                 = -10000
Global Const $DSFX_I3DL2REVERB_ROOMHF_MAX                 =  0
Global Const $DSFX_I3DL2REVERB_ROOMHF_DEFAULT             = -100
Global Const $DSFX_I3DL2REVERB_ROOMROLLOFFFACTOR_MIN      =  0.0
Global Const $DSFX_I3DL2REVERB_ROOMROLLOFFFACTOR_MAX      =  10.0
Global Const $DSFX_I3DL2REVERB_ROOMROLLOFFFACTOR_DEFAULT  =  0.0
Global Const $DSFX_I3DL2REVERB_DECAYTIME_MIN              =  0.1
Global Const $DSFX_I3DL2REVERB_DECAYTIME_MAX              =  20.0
Global Const $DSFX_I3DL2REVERB_DECAYTIME_DEFAULT          =  1.49
Global Const $DSFX_I3DL2REVERB_DECAYHFRATIO_MIN           =  0.1
Global Const $DSFX_I3DL2REVERB_DECAYHFRATIO_MAX           =  2.0
Global Const $DSFX_I3DL2REVERB_DECAYHFRATIO_DEFAULT       =  0.83
Global Const $DSFX_I3DL2REVERB_REFLECTIONS_MIN            = -10000
Global Const $DSFX_I3DL2REVERB_REFLECTIONS_MAX            =  1000
Global Const $DSFX_I3DL2REVERB_REFLECTIONS_DEFAULT        = -2602
Global Const $DSFX_I3DL2REVERB_REFLECTIONSDELAY_MIN       =  0.0
Global Const $DSFX_I3DL2REVERB_REFLECTIONSDELAY_MAX       =  0.3
Global Const $DSFX_I3DL2REVERB_REFLECTIONSDELAY_DEFAULT   =  0.007
Global Const $DSFX_I3DL2REVERB_REVERB_MIN                 = -10000
Global Const $DSFX_I3DL2REVERB_REVERB_MAX                 =  2000
Global Const $DSFX_I3DL2REVERB_REVERB_DEFAULT             =  200
Global Const $DSFX_I3DL2REVERB_REVERBDELAY_MIN            =  0.0
Global Const $DSFX_I3DL2REVERB_REVERBDELAY_MAX            =  0.1
Global Const $DSFX_I3DL2REVERB_REVERBDELAY_DEFAULT        =  0.011
Global Const $DSFX_I3DL2REVERB_DIFFUSION_MIN              =  0.0
Global Const $DSFX_I3DL2REVERB_DIFFUSION_MAX              =  100.0
Global Const $DSFX_I3DL2REVERB_DIFFUSION_DEFAULT          =  100.0
Global Const $DSFX_I3DL2REVERB_DENSITY_MIN                =  0.0
Global Const $DSFX_I3DL2REVERB_DENSITY_MAX                =  100.0
Global Const $DSFX_I3DL2REVERB_DENSITY_DEFAULT            =  100.0
Global Const $DSFX_I3DL2REVERB_HFREFERENCE_MIN            =  20.0
Global Const $DSFX_I3DL2REVERB_HFREFERENCE_MAX            =  20000.0
Global Const $DSFX_I3DL2REVERB_HFREFERENCE_DEFAULT        =  5000.0
Global Const $DSFX_I3DL2REVERB_QUALITY_MIN                =  0
Global Const $DSFX_I3DL2REVERB_QUALITY_MAX                =  3
Global Const $DSFX_I3DL2REVERB_QUALITY_DEFAULT            =  2

Global Const $DSFX_WAVESREVERB_INGAIN_MIN                 = -96.0
Global Const $DSFX_WAVESREVERB_INGAIN_MAX                 =  0.0
Global Const $DSFX_WAVESREVERB_INGAIN_DEFAULT             =  0.0
Global Const $DSFX_WAVESREVERB_REVERBMIX_MIN              = -96.0
Global Const $DSFX_WAVESREVERB_REVERBMIX_MAX              =  0.0
Global Const $DSFX_WAVESREVERB_REVERBMIX_DEFAULT          =  0.0
Global Const $DSFX_WAVESREVERB_REVERBTIME_MIN             =  0.001
Global Const $DSFX_WAVESREVERB_REVERBTIME_MAX             =  3000.0
Global Const $DSFX_WAVESREVERB_REVERBTIME_DEFAULT         =  1000.0
Global Const $DSFX_WAVESREVERB_HIGHFREQRTRATIO_MIN        =  0.001
Global Const $DSFX_WAVESREVERB_HIGHFREQRTRATIO_MAX        =  0.999
Global Const $DSFX_WAVESREVERB_HIGHFREQRTRATIO_DEFAULT    =  0.001

Global Const $DSCFX_AEC_MODE_PASS_THROUGH                      = 0x0
Global Const $DSCFX_AEC_MODE_HALF_DUPLEX                       = 0x1
Global Const $DSCFX_AEC_MODE_FULL_DUPLEX                       = 0x2
Global Const $DSCFX_AEC_STATUS_HISTORY_UNINITIALIZED           = 0x0
Global Const $DSCFX_AEC_STATUS_HISTORY_CONTINUOUSLY_CONVERGED  = 0x1
Global Const $DSCFX_AEC_STATUS_HISTORY_PREVIOUSLY_DIVERGED     = 0x2
Global Const $DSCFX_AEC_STATUS_CURRENTLY_CONVERGED             = 0x8

Global Const $DSCAPS_PRIMARYMONO      = 0x00000001
Global Const $DSCAPS_PRIMARYSTEREO    = 0x00000002
Global Const $DSCAPS_PRIMARY8BIT      = 0x00000004
Global Const $DSCAPS_PRIMARY16BIT     = 0x00000008
Global Const $DSCAPS_CONTINUOUSRATE   = 0x00000010
Global Const $DSCAPS_EMULDRIVER       = 0x00000020
Global Const $DSCAPS_CERTIFIED        = 0x00000040
Global Const $DSCAPS_SECONDARYMONO    = 0x00000100
Global Const $DSCAPS_SECONDARYSTEREO  = 0x00000200
Global Const $DSCAPS_SECONDARY8BIT    = 0x00000400
Global Const $DSCAPS_SECONDARY16BIT   = 0x00000800

Global Const $DSSCL_NORMAL        = 0x00000001
Global Const $DSSCL_PRIORITY      = 0x00000002
Global Const $DSSCL_EXCLUSIVE     = 0x00000003
Global Const $DSSCL_WRITEPRIMARY  = 0x00000004

Global Const $DSSPEAKER_DIRECTOUT        = 0x00000000
Global Const $DSSPEAKER_HEADPHONE        = 0x00000001
Global Const $DSSPEAKER_MONO             = 0x00000002
Global Const $DSSPEAKER_QUAD             = 0x00000003
Global Const $DSSPEAKER_STEREO           = 0x00000004
Global Const $DSSPEAKER_SURROUND         = 0x00000005
Global Const $DSSPEAKER_5POINT1          = 0x00000006
Global Const $DSSPEAKER_7POINT1          = 0x00000007
Global Const $DSSPEAKER_GEOMETRY_MIN     = 0x00000005
Global Const $DSSPEAKER_GEOMETRY_NARROW  = 0x0000000A
Global Const $DSSPEAKER_GEOMETRY_WIDE    = 0x00000014
Global Const $DSSPEAKER_GEOMETRY_MAX     = 0x000000B4

Global Const $DSBCAPS_PRIMARYBUFFER        = 0x00000001
Global Const $DSBCAPS_STATIC               = 0x00000002
Global Const $DSBCAPS_LOCHARDWARE          = 0x00000004
Global Const $DSBCAPS_LOCSOFTWARE          = 0x00000008
Global Const $DSBCAPS_CTRL3D               = 0x00000010
Global Const $DSBCAPS_CTRLFREQUENCY        = 0x00000020
Global Const $DSBCAPS_CTRLPAN              = 0x00000040
Global Const $DSBCAPS_CTRLVOLUME           = 0x00000080
Global Const $DSBCAPS_CTRLPOSITIONNOTIFY   = 0x00000100
Global Const $DSBCAPS_CTRLFX               = 0x00000200
Global Const $DSBCAPS_STICKYFOCUS          = 0x00004000
Global Const $DSBCAPS_GLOBALFOCUS          = 0x00008000
Global Const $DSBCAPS_GETCURRENTPOSITION2  = 0x00010000
Global Const $DSBCAPS_MUTE3DATMAXDISTANCE  = 0x00020000
Global Const $DSBCAPS_LOCDEFER             = 0x00040000

Global Const $DSBPLAY_LOOPING               = 0x00000001
Global Const $DSBPLAY_LOCHARDWARE           = 0x00000002
Global Const $DSBPLAY_LOCSOFTWARE           = 0x00000004
Global Const $DSBPLAY_TERMINATEBY_TIME      = 0x00000008
Global Const $DSBPLAY_TERMINATEBY_DISTANCE  = 0x000000010
Global Const $DSBPLAY_TERMINATEBY_PRIORITY  = 0x000000020

Global Const $DSBSTATUS_PLAYING      = 0x00000001
Global Const $DSBSTATUS_BUFFERLOST   = 0x00000002
Global Const $DSBSTATUS_LOOPING      = 0x00000004
Global Const $DSBSTATUS_LOCHARDWARE  = 0x00000008
Global Const $DSBSTATUS_LOCSOFTWARE  = 0x00000010
Global Const $DSBSTATUS_TERMINATED   = 0x00000020

Global Const $DSBLOCK_FROMWRITECURSOR  = 0x00000001
Global Const $DSBLOCK_ENTIREBUFFER     = 0x00000002

Global Const $DSBFREQUENCY_ORIGINAL  = 0
Global Const $DSBFREQUENCY_MIN       = 100
Global Const $DSBFREQUENCY_MAX       = 200000

Global Const $DSBPAN_LEFT    = -10000
Global Const $DSBPAN_CENTER  =  0
Global Const $DSBPAN_RIGHT   =  10000
Global Const $DSBVOLUME_MIN  = -10000
Global Const $DSBVOLUME_MAX  =  0

Global Const $DSBSIZE_MIN     = 4
Global Const $DSBSIZE_MAX     = 0x0FFFFFFF
Global Const $DSBSIZE_FX_MIN  = 150

Global Const $DS3DMODE_NORMAL        = 0x00000000
Global Const $DS3DMODE_HEADRELATIVE  = 0x00000001
Global Const $DS3DMODE_DISABLE       = 0x00000002

Global Const $DS3D_IMMEDIATE                 = 0x00000000
Global Const $DS3D_DEFERRED                  = 0x00000001
Global Const $DS3D_MINDISTANCEFACTOR         = 1.17549435E-38
Global Const $DS3D_MAXDISTANCEFACTOR         = 3.40282347E+38
Global Const $DS3D_DEFAULTDISTANCEFACTOR     = 1.0
Global Const $DS3D_MINROLLOFFFACTOR          = 0.0
Global Const $DS3D_MAXROLLOFFFACTOR          = 10.0
Global Const $DS3D_DEFAULTROLLOFFFACTOR      = 1.0
Global Const $DS3D_MINDOPPLERFACTOR          = 0.0
Global Const $DS3D_MAXDOPPLERFACTOR          = 10.0
Global Const $DS3D_DEFAULTDOPPLERFACTOR      = 1.0
Global Const $DS3D_DEFAULTMINDISTANCE        = 1.0
Global Const $DS3D_DEFAULTMAXDISTANCE        = 1000000000.0
Global Const $DS3D_MINCONEANGLE              = 0
Global Const $DS3D_MAXCONEANGLE              = 360
Global Const $DS3D_DEFAULTCONEANGLE          = 360
Global Const $DS3D_DEFAULTCONEOUTSIDEVOLUME  = 0

Global Const $DSCCAPS_EMULDRIVER       = 0x00000020
Global Const $DSCCAPS_CERTIFIED        = 0x00000040
Global Const $DSCCAPS_MULTIPLECAPTURE  = 0x00000001

Global Const $DSCBCAPS_WAVEMAPPED  = 0x80000000
Global Const $DSCBCAPS_CTRLFX      = 0x00000200

Global Const $DSCBLOCK_ENTIREBUFFER  = 0x00000001

Global Const $DSCBSTATUS_CAPTURING  = 0x00000001
Global Const $DSCBSTATUS_LOOPING    = 0x00000002

Global Const $DSCBSTART_LOOPING  = 0x00000001

Global Const $DSBPN_OFFSETSTOP  = 0xFFFFFFFF

Global Const $DS_CERTIFIED    = 0x00000000
Global Const $DS_UNCERTIFIED  = 0x00000001

Global Const $DSFXR_PRESENT      = 0
Global Const $DSFXR_LOCHARDWARE  = 1
Global Const $DSFXR_LOCSOFTWARE  = 2
Global Const $DSFXR_UNALLOCATED  = 3
Global Const $DSFXR_FAILED       = 4
Global Const $DSFXR_UNKNOWN      = 5
Global Const $DSFXR_SENDLOOP     = 6

;# I3DL2 Material Presets
Global Const $DSFX_I3DL2_MATERIAL_PRESET_SINGLEWINDOW  = 0 ; -2800, 0.71
Global Const $DSFX_I3DL2_MATERIAL_PRESET_DOUBLEWINDOW  = 1 ; -5000, 0.40
Global Const $DSFX_I3DL2_MATERIAL_PRESET_THINDOOR      = 2 ; -1800, 0.66
Global Const $DSFX_I3DL2_MATERIAL_PRESET_THICKDOOR     = 3 ; -4400, 0.64
Global Const $DSFX_I3DL2_MATERIAL_PRESET_WOODWALL      = 4 ; -4000, 0.50
Global Const $DSFX_I3DL2_MATERIAL_PRESET_BRICKWALL     = 5 ; -5000, 0.60
Global Const $DSFX_I3DL2_MATERIAL_PRESET_STONEWALL     = 6 ; -6000, 0.68
Global Const $DSFX_I3DL2_MATERIAL_PRESET_CURTAIN       = 7 ; -1200, 0.15

;# I3DL2 Material Presets
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_DEFAULT          = 0  ; -1000, -100, 0.0, 1.49, 0.83, -2602, 0.007,   200, 0.011, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_GENERIC          = 1  ; -1000, -100, 0.0, 1.49, 0.83, -2602, 0.007,   200, 0.011, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_PADDEDCELL       = 2  ; -1000,-6000, 0.0, 0.17, 0.10, -1204, 0.001,   207, 0.002, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_ROOM             = 3  ; -1000, -454, 0.0, 0.40, 0.83, -1646, 0.002,    53, 0.003, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_BATHROOM         = 4  ; -1000,-1200, 0.0, 1.49, 0.54,  -370, 0.007,  1030, 0.011, 100.0,  60.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_LIVINGROOM       = 5  ; -1000,-6000, 0.0, 0.50, 0.10, -1376, 0.003, -1104, 0.004, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_STONEROOM        = 6  ; -1000, -300, 0.0, 2.31, 0.64,  -711, 0.012,    83, 0.017, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_AUDITORIUM       = 7  ; -1000, -476, 0.0, 4.32, 0.59,  -789, 0.020,  -289, 0.030, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_CONCERTHALL      = 8  ; -1000, -500, 0.0, 3.92, 0.70, -1230, 0.020,    -2, 0.029, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_CAVE             = 9  ; -1000,    0, 0.0, 2.91, 1.30,  -602, 0.015,  -302, 0.022, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_ARENA            = 10 ; -1000, -698, 0.0, 7.24, 0.33, -1166, 0.020,    16, 0.030, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_HANGAR           = 11 ; -1000,-1000, 0.0,10.05, 0.23,  -602, 0.020,   198, 0.030, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_CARPETEDHALLWAY  = 12 ; -1000,-4000, 0.0, 0.30, 0.10, -1831, 0.002, -1630, 0.030, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_HALLWAY          = 13 ; -1000, -300, 0.0, 1.49, 0.59, -1219, 0.007,   441, 0.011, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_STONECORRIDOR    = 14 ; -1000, -237, 0.0, 2.70, 0.79, -1214, 0.013,   395, 0.020, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_ALLEY            = 15 ; -1000, -270, 0.0, 1.49, 0.86, -1204, 0.007,    -4, 0.011, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_FOREST           = 16 ; -1000,-3300, 0.0, 1.49, 0.54, -2560, 0.162,  -613, 0.088,  79.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_CITY             = 17 ; -1000, -800, 0.0, 1.49, 0.67, -2273, 0.007, -2217, 0.011,  50.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_MOUNTAINS        = 18 ; -1000,-2500, 0.0, 1.49, 0.21, -2780, 0.300, -2014, 0.100,  27.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_QUARRY           = 19 ; -1000,-1000, 0.0, 1.49, 0.83,-10000, 0.061,   500, 0.025, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_PLAIN            = 20 ; -1000,-2000, 0.0, 1.49, 0.50, -2466, 0.179, -2514, 0.100,  21.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_PARKINGLOT       = 21 ; -1000,    0, 0.0, 1.65, 1.50, -1363, 0.008, -1153, 0.012, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_SEWERPIPE        = 22 ; -1000,-1000, 0.0, 2.81, 0.14,   429, 0.014,   648, 0.021,  80.0,  60.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_UNDERWATER       = 23 ; -1000,-4000, 0.0, 1.49, 0.10,  -449, 0.007,  1700, 0.011, 100.0, 100.0, 5000.0
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_SMALLROOM        = 24
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_MEDIUMROOM       = 25
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_LARGEROOM        = 26
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_MEDIUMHALL       = 27
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_LARGEHALL        = 28
Global Const $DSFX_I3DL2_ENVIRONMENT_PRESET_PLATE            = 29

Global Const $SPEAKER_FRONT_LEFT            = 0x00000001
Global Const $SPEAKER_FRONT_RIGHT           = 0x00000002
Global Const $SPEAKER_FRONT_CENTER          = 0x00000004
Global Const $SPEAKER_LOW_FREQUENCY         = 0x00000008
Global Const $SPEAKER_BACK_LEFT             = 0x00000010
Global Const $SPEAKER_BACK_RIGHT            = 0x00000020
Global Const $SPEAKER_FRONT_LEFT_OF_CENTER  = 0x00000040
Global Const $SPEAKER_FRONT_RIGHT_OF_CENTER = 0x00000080
Global Const $SPEAKER_BACK_CENTER           = 0x00000100
Global Const $SPEAKER_SIDE_LEFT             = 0x00000200
Global Const $SPEAKER_SIDE_RIGHT            = 0x00000400
Global Const $SPEAKER_TOP_CENTER            = 0x00000800
Global Const $SPEAKER_TOP_FRONT_LEFT        = 0x00001000
Global Const $SPEAKER_TOP_FRONT_CENTER      = 0x00002000
Global Const $SPEAKER_TOP_FRONT_RIGHT       = 0x00004000
Global Const $SPEAKER_TOP_BACK_LEFT         = 0x00008000
Global Const $SPEAKER_TOP_BACK_CENTER       = 0x00010000
Global Const $SPEAKER_TOP_BACK_RIGHT        = 0x00020000
Global Const $SPEAKER_RESERVED              = 0x7FFC0000
Global Const $SPEAKER_ALL                   = 0x80000000

Global Const $SPEAKER_MONO             = $SPEAKER_FRONT_CENTER
Global Const $SPEAKER_STEREO           = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT)
Global Const $SPEAKER_2POINT1          = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_LOW_FREQUENCY)
Global Const $SPEAKER_SURROUND         = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_FRONT_CENTER, $SPEAKER_BACK_CENTER)
Global Const $SPEAKER_QUAD             = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_BACK_LEFT, $SPEAKER_BACK_RIGHT)
Global Const $SPEAKER_4POINT1          = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_LOW_FREQUENCY, $SPEAKER_BACK_LEFT, $SPEAKER_BACK_RIGHT)
Global Const $SPEAKER_5POINT1          = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_FRONT_CENTER, $SPEAKER_LOW_FREQUENCY, $SPEAKER_BACK_LEFT, $SPEAKER_BACK_RIGHT)
Global Const $SPEAKER_7POINT1          = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_FRONT_CENTER, $SPEAKER_LOW_FREQUENCY, $SPEAKER_BACK_LEFT, $SPEAKER_BACK_RIGHT, $SPEAKER_FRONT_LEFT_OF_CENTER, $SPEAKER_FRONT_RIGHT_OF_CENTER)
Global Const $SPEAKER_5POINT1_SURROUND = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_FRONT_CENTER, $SPEAKER_LOW_FREQUENCY, $SPEAKER_SIDE_LEFT, $SPEAKER_SIDE_RIGHT)
Global Const $SPEAKER_7POINT1_SURROUND = BitOR($SPEAKER_FRONT_LEFT, $SPEAKER_FRONT_RIGHT, $SPEAKER_FRONT_CENTER, $SPEAKER_LOW_FREQUENCY, $SPEAKER_BACK_LEFT, $SPEAKER_BACK_RIGHT, $SPEAKER_SIDE_LEFT , $SPEAKER_SIDE_RIGHT)

Global Const $DSEnumCallback_Return = "bool"
Global Const $DSEnumCallback_Params = "ptr;str;str;ptr"

; ###############################################################################################################################
; # DirectSound Structures
; ###############################################################################################################################
Global Const $tagDSWAVEFORMATEX         = "struct; align 2; word FormatTag; word Channels; uint SamplesPerSec; uint AvgBytesPerSec; word BlockAlign; word BitsPerSample; word Size; endstruct;"
Global Const $tagDSWAVEFORMATEXTENSIBLE = "struct; align 2; " & $tagDSWAVEFORMATEX & " ushort ValidBitsPerSample; uint ChannelMask; byte SubFormat[16]; endstruct;"
Global Const $tagDSMPEGLAYER3WAVEFORMAT = "struct; align 2; " & $tagDSWAVEFORMATEX & " ushort ID; uint Flags; ushort BlockSize; ushort FramesPerBlock; ushort CodecDelay; endstruct;"

Global Const $tagDSFXGargle         = "struct; uint RateHz; uint WaveShape; endstruct;"
Global Const $tagDSFXChorus         = "struct; float WetDryMix; float Depth; float Feedback; float Frequency; int Waveform; float Delay; int Phase; endstruct;"
Global Const $tagDSFXFlanger        = "struct; float WetDryMix; float Depth; float Feedback; float Frequency; int Waveform; float Delay; int Phase; endstruct;"
Global Const $tagDSFXEcho           = "struct; float WetDryMix; float Feedback; float LeftDelay; float RightDelay; int PanDelay; endstruct;"
Global Const $tagDSFXDistortion     = "struct; float Gain; float Edge; float PostEQCenterFrequency; float PostEQBandwidth; float PreLowpassCutoff; endstruct;"
Global Const $tagDSFXCompressor     = "struct; float Gain; float Attack; float Release; float Threshold; float Ratio; float Predelay; endstruct;"
Global Const $tagDSFXParamEq        = "struct; float Center; float Bandwidth; float Gain; endstruct;"
Global Const $tagDSFXI3DL2Reverb    = "struct; int Room; int RoomHF; float RoomRolloffFactor; float DecayTime; float DecayHFRatio; int Reflections; float ReflectionsDelay; int Reverb; float ReverbDelay; float Diffusion; float Density; float HFReference; endstruct;"
Global Const $tagDSFXWavesReverb    = "struct; float InGain; float ReverbMix; float ReverbTime; float HighFreqRTRatio; endstruct;"
Global Const $tagDSCFXAec           = "struct; bool Enable; bool NoiseFill; uint Mode; endstruct;"
Global Const $tagDSCFXNoiseSuppress = "struct; bool Enable; endstruct;"

Global Const $tagD3DVECTOR = "struct; float X; float Y; float Z; endstruct;"

Global Const $tagDSCAPS            = "struct; uint Size; uint Flags; uint MinSecondarySampleRate; uint MaxSecondarySampleRate; uint PrimaryBuffers; uint MaxHwMixingAllBuffers; uint MaxHwMixingStaticBuffers; uint MaxHwMixingStreamingBuffers; uint FreeHwMixingAllBuffers; uint FreeHwMixingStaticBuffers; uint FreeHwMixingStreamingBuffers; uint MaxHw3DAllBuffers; uint MaxHw3DStaticBuffers; uint MaxHw3DStreamingBuffers; uint FreeHw3DAllBuffers; uint FreeHw3DStaticBuffers; uint FreeHw3DStreamingBuffers; uint TotalHwMemBytes; uint FreeHwMemBytes; uint MaxContigFreeHwMemBytes; uint UnlockTransferRateHwBuffers; uint PlayCpuOverheadSwBuffers; uint Reserved1; uint Reserved2; endstruct;"
Global Const $tagDSBCAPS           = "struct; uint Size; uint Flags; uint BufferBytes; uint UnlockTransferRate; uint PlayCpuOverhead; endstruct;"
Global Const $tagDSEFFECTDESC      = "struct; uint Size; uint Flags; byte DSFXClass[16]; uint_ptr Reserved1; uint_ptr Reserved2; endstruct;"
Global Const $tagDSCEFFECTDESC     = "struct; uint Size; uint Flags; byte DSCFXClass[16]; byte DSCFXInstance[16]; uint Reserved1; uint Reserved2; endstruct;"
Global Const $tagDSBUFFERDESC      = "struct; uint Size; uint Flags; uint BufferBytes; uint Reserved; ptr WaveFormatEX; byte p3DAlgorithm[16]; endstruct;"
Global Const $tagDSBUFFERDESC1     = "struct; uint Size; uint Flags; uint BufferBytes; uint Reserved; ptr WaveFormatEX; endstruct;"
Global Const $tagDS3DBUFFER        = "struct; uint Size; struct; float PositionX; float PositionY; float PositionZ; endstruct; struct; float VelocityX; float VelocityY; float VelocityZ; endstruct; uint InsideConeAngle; uint OutsideConeAngle; struct; float ConeOrientationX; float ConeOrientationY; float ConeOrientationZ; endstruct; int ConeOutsideVolume; float flMinDistance; float flMaxDistance; uint Mode; endstruct;"
Global Const $tagDS3DLISTENER      = "struct; uint Size; struct; float PositionX; float PositionY; float PositionZ; endstruct; struct; float VelocityX; float VelocityY; float VelocityZ; endstruct; struct; float OrientFrontX; float OrientFrontY; float OrientFrontZ; endstruct; struct; float OrientTopX; float OrientTopY; float OrientTopZ; endstruct; float flDistanceFactor; float flRolloffFactor; float flDopplerFactor; endstruct;"
Global Const $tagDSCCAPS           = "struct; uint Size; uint Flags; uint Formats; uint Channels; endstruct;"
Global Const $tagDSCBUFFERDESC1    = "struct; uint Size; uint Flags; uint BufferBytes; uint Reserved; ptr WaveFormatEX; endstruct;"
Global Const $tagDSCBUFFERDESC     = "struct; uint Size; uint Flags; uint BufferBytes; uint Reserved; ptr WaveFormatEX; uint FXCount; ptr DSCFXDesc; endstruct;"
Global Const $tagDSCBCAPS          = "struct; uint Size; uint Flags; uint BufferBytes; uint Reserved; endstruct;"
Global Const $tagDSBPOSITIONNOTIFY = "struct; uint Offset; handle EventNotify; endstruct;"

; ###############################################################################################################################
; # DirectSound GUID
; ###############################################################################################################################
Global Const $sCLSID_DirectSound           = "{47D4D946-62E8-11CF-93BC-444553540000}"
Global Const $sCLSID_DirectSound8          = "{3901CC3F-84B5-4FA4-BA35-AA8172B8A09B}"
Global Const $sCLSID_DirectSoundCapture    = "{B0210780-89CD-11D0-AF08-00A0C925CD16}"
Global Const $sCLSID_DirectSoundCapture8   = "{E4BCAC13-7F99-4908-9A8E-74E3BF24B6E1}"
Global Const $sCLSID_DirectSoundFullDuplex = "{FEA4300C-7959-4147-B26A-2377B9E7A91D}"

Global Const $sDSDEVID_DefaultPlayback      = "{DEF00000-9C6D-47ED-AAF1-4DDA8F2B5C03}"
Global Const $sDSDEVID_DefaultCapture       = "{DEF00001-9C6D-47ED-AAF1-4DDA8F2B5C03}"
Global Const $sDSDEVID_DefaultVoicePlayback = "{DEF00002-9C6D-47ED-AAF1-4DDA8F2B5C03}"
Global Const $sDSDEVID_DefaultVoiceCapture  = "{DEF00003-9C6D-47ED-AAF1-4DDA8F2B5C03}"

Global Const $sGUID_All_Objects = "{AA114DE5-C262-4169-A1C8-23D698CC73B5}"

Global Const $sIID_IReferenceClock            = "{56A86897-0AD4-11CE-B03A-0020AF0BA770}"
Global Const $sIID_IDirectSound               = "{279AFA83-4981-11CE-A521-0020AF0BE560}"
Global Const $sIID_IDirectSound8              = "{C50A7E93-F395-4834-9EF6-7FA99DE50966}"
Global Const $sIID_IDirectSoundBuffer         = "{279AFA85-4981-11CE-A521-0020AF0BE560}"
Global Const $sIID_IDirectSoundBuffer8        = "{6825A449-7524-4D82-920F-50E36AB3AB1E}"
Global Const $sIID_IDirectSound3DListener     = "{279AFA84-4981-11CE-A521-0020AF0BE560}"
Global Const $sIID_IDirectSound3DBuffer       = "{279AFA86-4981-11CE-A521-0020AF0BE560}"
Global Const $sIID_IDirectSoundCapture        = "{B0210781-89CD-11D0-AF08-00A0C925CD16}"
Global Const $sIID_IDirectSoundCaptureBuffer  = "{B0210782-89CD-11D0-AF08-00A0C925CD16}"
Global Const $sIID_IDirectSoundCaptureBuffer8 = "{990DF4-DBB-4872-833E-6D303E80AEB6}"
Global Const $sIID_IDirectSoundNotify         = "{B0210783-89CD-11D0-AF08-00A0C925CD16}"
Global Const $sIID_IKsPropertySet             = "{31EFAC30-515C-11D0-A9AA-00AA0061BE93}"

Global Const $sIID_IDirectSoundFXGargle               = "{D616F352-D622-11CE-AAC5-0020AF0B99A3}"
Global Const $sIID_IDirectSoundFXChorus               = "{880842E3-145F-43E6-A934-A71806E50547}"
Global Const $sIID_IDirectSoundFXFlanger              = "{903E9878-2C92-4072-9B2C-EA68F5396783}"
Global Const $sIID_IDirectSoundFXEcho                 = "{8BD28EDF-50DB-4E92-A2BD-445488D1ED42}"
Global Const $sIID_IDirectSoundFXDistortion           = "{8ECF4326-455F-4D8B-BDA9-8D5D3E9E3E0B}"
Global Const $sIID_IDirectSoundFXCompressor           = "{4BBD1154-62F6-4E2C-A15C-D3B6C417F7A0}"
Global Const $sIID_IDirectSoundFXParamEq              = "{C03CA9FE-FE90-4204-8078-82334CD177DA}"
Global Const $sIID_IDirectSoundFXI3DL2Reverb          = "{4B166A6A-0D66-43F3-80E3-EE6280DEE1A4}"
Global Const $sIID_IDirectSoundFXWavesReverb          = "{46858C3A-0DC6-45E3-B760-D4EEF16CB325}"
Global Const $sIID_IDirectSoundCaptureFXAec           = "{AD74143D-903D-4AB7-8066-28D363036D65}"
Global Const $sIID_IDirectSoundCaptureFXNoiseSuppress = "{ED311E41-FBAE-4175-9625-CD0854F693CA}"
Global Const $sIID_IDirectSoundFullDuplex             = "{EDCB4C7A-DAAB-4216-A42E-6C50596DDC1D}"

Global Const $sDS3DALG_DEFAULT           = "{00000000-0000-0000-0000-000000000000}"
Global Const $sDS3DALG_NO_VIRTUALIZATION = "{C241333F-1C1B-11D2-94F5-00C04FC28ACA}"
Global Const $sDS3DALG_HRTF_FULL         = "{C2413340-1C1B-11D2-94F5-00C04FC28ACA}"
Global Const $sDS3DALG_HRTF_LIGHT        = "{C2413342-1C1B-11D2-94F5-00C04FC28ACA}"

Global Const $sGUID_DSFX_STANDARD_GARGLE      = "{DAFD8210-5711-4B91-9FE3-F75B7AE279BF}"
Global Const $sGUID_DSFX_STANDARD_CHORUS      = "{EFE6629C-81F7-4281-BD91-C9D604A95AF6}"
Global Const $sGUID_DSFX_STANDARD_FLANGER     = "{EFCA3D92-DFD8-4672-A603-7420894BAD98}"
Global Const $sGUID_DSFX_STANDARD_ECHO        = "{EF3E932C-D40B-4F51-8CCF-3F98F1B29D5D}"
Global Const $sGUID_DSFX_STANDARD_DISTORTION  = "{EF114C90-CD1D-484E-96E5-09CFAF912A21}"
Global Const $sGUID_DSFX_STANDARD_COMPRESSOR  = "{EF011F79-4000-406D-87AF-BFFB3FC39D57}"
Global Const $sGUID_DSFX_STANDARD_PARAMEQ     = "{120CED89-3BF4-4173-A132-3CB406CF3231}"
Global Const $sGUID_DSFX_STANDARD_I3DL2REVERB = "{EF985E71-D5C7-42D4-BA4D-2D073E2E96F4}"
Global Const $sGUID_DSFX_WAVES_REVERB         = "{87FC0268-9A55-4360-95AA-004A1D9DE26C}"

Global Const $sGUID_DSCFX_CLASS_AEC  = "{BF963D80L-C559-11D0-8A2B-00A0C9255AC1}"
Global Const $sGUID_DSCFX_MS_AEC     = "{CDEBB919-379A-488A-8765-F53CFD36DE40}"
Global Const $sGUID_DSCFX_SYSTEM_AEC = "{1C22C56D-9879-4F5B-A389-27996DDC2810}"
Global Const $sGUID_DSCFX_CLASS_NS   = "{E07F903F-62FD-4E60-8CDD-DEA7236665B5}"
Global Const $sGUID_DSCFX_MS_NS      = "{11C5C73B-66E9-4BA1-A0BA-E814C6EED92D}"
Global Const $sGUID_DSCFX_SYSTEM_NS  = "{5AB0882E-7274-4516-877D-4EEE99BA4FD0}"

; ###############################################################################################################################
; # DirectSound InterfaceDescription
; ###############################################################################################################################

Global Const $tagIReferenceClock = "GetTime hresult(int64*);" & _
		"AdviseTime hresult(int64;int64;handle;uint*);" & _
		"AdvisePeriodic hresult(int64;int64;handle;uint*);" & _
		"Unadvise hresult(uint);"

Global Const $tagIDirectSound = "CreateSoundBuffer hresult(struct*;ptr*;struct*);" & _
		"GetCaps hresult(struct*);" & _
		"DuplicateSoundBuffer hresult(struct*;ptr*);" & _
		"SetCooperativeLevel hresult(hwnd;uint);" & _
		"Compact hresult();" & _
		"GetSpeakerConfig hresult(uint*);" & _
		"SetSpeakerConfig hresult(uint);" & _
		"Initialize hresult(struct*);"

Global Const $tagIDirectSound8 = $tagIDirectSound & _
		"VerifyCertification hresult(uint*);"

Global Const $tagIDirectSoundBuffer = "GetCaps hresult(struct*);" & _
		"GetCurrentPosition hresult(uint*;uint*);" & _
		"GetFormat hresult(struct*;uint;uint*);" & _
		"GetVolume hresult(int*);" & _
		"GetPan hresult(int*);" & _
		"GetFrequency hresult(uint*);" & _
		"GetStatus hresult(uint*);" & _
		"Initialize hresult(struct*;struct*);" & _
		"Lock hresult(uint;uint;ptr*;uint*;ptr*;uint*;uint);" & _
		"Play hresult(uint;uint;uint);" & _
		"SetCurrentPosition hresult(uint);" & _
		"SetFormat hresult(struct*);" & _
		"SetVolume hresult(int);" & _
		"SetPan hresult(int);" & _
		"SetFrequency hresult(uint);" & _
		"Stop hresult();" & _
		"Unlock hresult(struct*;uint;struct*;uint);" & _
		"Restore hresult();"

Global Const $tagIDirectSoundBuffer8 = $tagIDirectSoundBuffer & _
		"SetFX hresult(uint;struct*;struct*);" & _
		"AcquireResources hresult(uint;uint;uint*);" & _
		"GetObjectInPath hresult(struct*;uint;struct*;ptr*);"

Global Const $tagIDirectSound3DListener = "GetAllParameters hresult(struct*);" & _
		"GetDistanceFactor hresult(float*);" & _
		"GetDopplerFactor hresult(float*);" & _
		"GetOrientation hresult(struct*;struct*);" & _
		"GetPosition hresult(struct*);" & _
		"GetRolloffFactor hresult(float*);" & _
		"GetVelocity hresult(struct*);" & _
		"SetAllParameters hresult(struct*;uint);" & _
		"SetDistanceFactor hresult(float;uint);" & _
		"SetDopplerFactor hresult(float;uint);" & _
		"SetOrientation hresult(float;float;float;float;float;float;uint);" & _
		"SetPosition hresult(float;float;float;uint);" & _
		"SetRolloffFactor hresult(float;uint);" & _
		"SetVelocity hresult(float;float;float;uint);" & _
		"CommitDeferredSettings hresult();"

Global Const $tagIDirectSound3DBuffer = "GetAllParameters hresult(struct*);" & _
		"GetConeAngles hresult(uint*;uint*);" & _
		"GetConeOrientation hresult(struct*);" & _
		"GetConeOutsideVolume hresult(int*);" & _
		"GetMaxDistance hresult(float*);" & _
		"GetMinDistance hresult(float*);" & _
		"GetMode hresult(uint*);" & _
		"GetPosition hresult(struct*);" & _
		"GetVelocity hresult(struct*);" & _
		"SetAllParameters hresult(struct*;uint);" & _
		"SetConeAngles hresult(uint;uint;uint);" & _
		"SetConeOrientation hresult(float;float;float;uint);" & _
		"SetConeOutsideVolume hresult(int;uint);" & _
		"SetMaxDistance hresult(float;uint);" & _
		"SetMinDistance hresult(float;uint);" & _
		"SetMode hresult(uint;uint);" & _
		"SetPosition hresult(float;float;float;uint);" & _
		"SetVelocity hresult(float;float;float;uint);"

Global Const $tagIDirectSoundCapture = "CreateCaptureBuffer hresult(struct*;ptr*;struct*);" & _
		"GetCaps hresult(struct*);" & _
		"Initialize hresult(struct*);"

Global Const $tagIDirectSoundCaptureBuffer = "GetCaps hresult(struct*);" & _
		"GetCurrentPosition hresult(uint*;uint*);" & _
		"GetFormat hresult(struct*;uint;uint*);" & _
		"GetStatus hresult(uint*);" & _
		"Initialize hresult(struct*;struct*);" & _
		"Lock hresult(uint;uint;ptr*;uint*;ptr*;uint*;uint);" & _
		"Start hresult(uint);" & _
		"Stop hresult();" & _
		"Unlock hresult(struct*;uint;struct*;uint);"

Global Const $tagIDirectSoundCaptureBuffer8 = $tagIDirectSoundCaptureBuffer & _
		"GetObjectInPath hresult(struct*;uint;struct*;ptr*);" & _
		"GetFXStatus hresult(uint;uint*);"

Global Const $tagIDirectSoundNotify = "SetNotificationPositions hresult(uint;struct*);"

Global Const $tagIKsPropertySet = "Get hresult(struct*;uint;struct*;uint;struct*;uint;uint*);" & _
		"Set hresult(struct*;uint;struct*;uint;struct*;uint);" & _
		"QuerySupport hresult(struct*;uint;uint*);"

Global Const $tagIDirectSoundFX = "SetAllParameters hresult(struct*);" & _
		"GetAllParameters hresult(struct*);"
Global Const $tagIDirectSoundFXGargle = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXChorus = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXFlanger = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXEcho = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXDistortion = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXCompressor = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXParamEq = $tagIDirectSoundFX
Global Const $tagIDirectSoundFXWavesReverb = $tagIDirectSoundFX

Global Const $tagIDirectSoundFXI3DL2Reverb = $tagIDirectSoundFX & _
		"SetPreset hresult(uint);" & _
		"GetPreset hresult(uint*);" & _
		"SetQuality hresult(int);" & _
		"GetQuality hresult(int*);"

Global Const $tagIDirectSoundCaptureFXAec = $tagIDirectSoundFX & _
		"GetStatus hresult(uint*);" & _
		"Reset hresult();"

Global Const $tagIDirectSoundCaptureFXNoiseSuppress = $tagIDirectSoundFX & _
		"Reset hresult();"

Global Const $tagIDirectSoundFullDuplex = "Initialize hresult(struct*;struct*;struct*;struct*;hwnd;uint;ptr*;ptr*);"

; ###############################################################################################################################
; # DMO Error Codes
; ###############################################################################################################################

Global Const $DMO_E_INVALIDSTREAMINDEX = 0x80040201
Global Const $DMO_E_INVALIDTYPE        = 0x80040202
Global Const $DMO_E_TYPE_NOT_SET       = 0x80040203
Global Const $DMO_E_NOTACCEPTING       = 0x80040204
Global Const $DMO_E_TYPE_NOT_ACCEPTED  = 0x80040205
Global Const $DMO_E_NO_MORE_ITEMS      = 0x80040206

; ###############################################################################################################################
; # DMO Constants
; ###############################################################################################################################

;# DMO_INPUT_DATA_BUFFER_FLAGS
Global Const $DMO_INPUT_DATA_BUFFERF_SYNCPOINT  = 0x1
Global Const $DMO_INPUT_DATA_BUFFERF_TIME       = 0x2
Global Const $DMO_INPUT_DATA_BUFFERF_TIMELENGTH = 0x4

;# DMO_OUTPUT_DATA_BUFFER_FLAGS
Global Const $DMO_OUTPUT_DATA_BUFFERF_SYNCPOINT = 0x1
Global Const $DMO_OUTPUT_DATA_BUFFERF_TIME       = 0x2
Global Const $DMO_OUTPUT_DATA_BUFFERF_TIMELENGTH = 0x4
Global Const $DMO_OUTPUT_DATA_BUFFERF_INCOMPLETE = 0x1000000

;# DMO_INPUT_STATUS_FLAGS
Global Const $DMO_INPUT_STATUSF_ACCEPT_DATA = 0x1

;# DMO_INPUT_STREAM_INFO_FLAGS
Global Const $DMO_INPUT_STREAMF_WHOLE_SAMPLES            = 0x1
Global Const $DMO_INPUT_STREAMF_SINGLE_SAMPLE_PER_BUFFER = 0x2
Global Const $DMO_INPUT_STREAMF_FIXED_SAMPLE_SIZE        = 0x4
Global Const $DMO_INPUT_STREAMF_HOLDS_BUFFERS            = 0x8

;# DMO_OUTPUT_STREAM_INFO_FLAGS
Global Const $DMO_OUTPUT_STREAMF_WHOLE_SAMPLES            = 0x1
Global Const $DMO_OUTPUT_STREAMF_SINGLE_SAMPLE_PER_BUFFER = 0x2
Global Const $DMO_OUTPUT_STREAMF_FIXED_SAMPLE_SIZE        = 0x4
Global Const $DMO_OUTPUT_STREAMF_DISCARDABLE              = 0x8
Global Const $DMO_OUTPUT_STREAMF_OPTIONAL                 = 0x10

;# DMO_SET_TYPE_FLAGS
Global Const $DMO_SET_TYPEF_TEST_ONLY = 0x1
Global Const $DMO_SET_TYPEF_CLEAR     = 0x2

;# DMO_PROCESS_OUTPUT_FLAGS
Global Const $DMO_PROCESS_OUTPUT_DISCARD_WHEN_NO_BUFFER = 0x1

;# DMO_VIDEO_OUTPUT_STREAM_FLAGS
Global Const $DMO_VOSF_NEEDS_PREVIOUS_SAMPLE = 0x1

Global Const $DMO_ENUMF_INCLUDE_KEYED = 1

Global Const $DMO_QUALITY_STATUS_ENABLED = 0x1

Global Const $DMO_INPLACE_NORMAL = 0x0
Global Const $DMO_INPLACE_ZERO   = 0x1

Global Const $CLSCTX_INPROC_SERVER           = 0x1
Global Const $CLSCTX_INPROC_HANDLER          = 0x2
Global Const $CLSCTX_LOCAL_SERVER            = 0x4
Global Const $CLSCTX_INPROC_SERVER16         = 0x8
Global Const $CLSCTX_REMOTE_SERVER           = 0x10
Global Const $CLSCTX_INPROC_HANDLER16        = 0x20
Global Const $CLSCTX_RESERVED1               = 0x40
Global Const $CLSCTX_RESERVED2               = 0x80
Global Const $CLSCTX_RESERVED3               = 0x100
Global Const $CLSCTX_RESERVED4               = 0x200
Global Const $CLSCTX_NO_CODE_DOWNLOAD        = 0x400
Global Const $CLSCTX_RESERVED5               = 0x800
Global Const $CLSCTX_NO_CUSTOM_MARSHAL       = 0x1000
Global Const $CLSCTX_ENABLE_CODE_DOWNLOAD    = 0x2000
Global Const $CLSCTX_NO_FAILURE_LOG          = 0x4000
Global Const $CLSCTX_DISABLE_AAA             = 0x8000
Global Const $CLSCTX_ENABLE_AAA              = 0x10000
Global Const $CLSCTX_FROM_DEFAULT_CONTEXT    = 0x20000
Global Const $CLSCTX_ACTIVATE_32_BIT_SERVER  = 0x40000
Global Const $CLSCTX_ACTIVATE_64_BIT_SERVER  = 0x80000
Global Const $CLSCTX_ENABLE_CLOAKING         = 0x100000
Global Const $CLSCTX_APPCONTAINER            = 0x400000
Global Const $CLSCTX_ACTIVATE_AAA_AS_IU      = 0x800000
Global Const $CLSCTX_PS_DLL                  = 0x80000000

; # MP_TYPE
Global Const $MPT_INT   = 0
Global Const $MPT_FLOAT = 1
Global Const $MPT_BOOL  = 2
Global Const $MPT_ENUM  = 3
Global Const $MPT_MAX   = 4

Global Const $MPBOOL_TRUE = 1
Global Const $MPBOOL_FALSE = 0

; # MP_CURVE_TYPE
Global Const $MP_CURVE_JUMP      = 0x1
Global Const $MP_CURVE_LINEAR    = 0x2
Global Const $MP_CURVE_SQUARE    = 0x4
Global Const $MP_CURVE_INVSQUARE = 0x8
Global Const $MP_CURVE_SINE      = 0x10

Global Const $DWORD_ALLPARAMS = -1

; ###############################################################################################################################
; # DMO Structures
; ###############################################################################################################################

Global Const $tagDMO_MEDIA_TYPE = "struct; byte MajorType[16]; byte SubType[16]; bool FixedSizeSamples; bool TemporalCompression; uint SampleSize; byte FormatType[16]; ptr pUnk; uint Format; ptr pFormat; endstruct;"
Global Const $tagDMO_OUTPUT_DATA_BUFFER = "struct; ptr Buffer; uint Status; int64 TimeStamp; int64 TimeLength; endstruct;"
Global Const $tagDMO_MEDIABUFFER = "struct; ptr Vtbl; int RefCnt; bool IsFree; int Length; int Size; ptr Buffer; ptr QueryInterface; ptr AddRef; ptr Release; ptr SetLength; ptr GetMaxLength; ptr GetBufferAndLength; " & _
		" ptr pQueryInterface; ptr pAddRef; ptr pRelease; ptr pSetLength; ptr pGetMaxLength; ptr pGetBufferAndLength; endstruct;"

Global Const $tagMP_PARAMINFO = "struct; uint Type; uint Caps; float MinValue; float MaxValue; float NeutralValue; wchar UnitText[32]; wchar Label[32]; endstruct;"
Global Const $tagMP_ENVELOPE_SEGMENT = "struct; int64 TimeStart; int64 TimeEnd; float ValueStart; float ValueEnd; uint CurveType; uint Flags; endstruct;"

; ###############################################################################################################################
; # IMediaBuffer ASM Interface
; ###############################################################################################################################

Global Const $bDMO_MBQueryInterfaceASM       = "0x538B4424088B5C2410890383400401B8000000005BC20C00"
Global Const $bDMO_MBAddRefASM               = "0x8B442404834004018B4004C20400"
Global Const $bDMO_MBReleaseASM              = "0x8B44240483680401837804007F07C74008010000008B4004C20400"
Global Const $bDMO_MBSetLengthASM            = "0x538B4424088B5C240C89580CB8000000005BC20800"
Global Const $bDMO_MBGetMaxLengthASM         = "0x53528B44240C8B5C241083FB00740C8B50108913B800000000EB05B8034000805A5BC20800"
Global Const $bDMO_MBGetBufferAndLengthASM   = "0x5351528B4424108B5C24148B4C241883FB0074058B5014891383F900740C8B500C8911B800000000EB0FB80000000083FB007505B8034000805A595BC20C00"

Global Const $bDMO_MBQueryInterfaceASM64     = "0x49890883410801B800000000C3"
Global Const $bDMO_MBAddRefASM64             = "0x834108018B4108C3"
Global Const $bDMO_MBReleaseASM64            = "0x83690801837908007F07C7410C010000008B4108C3"
Global Const $bDMO_MBSetLengthASM64          = "0x895110B800000000C3"
Global Const $bDMO_MBGetMaxLengthASM64       = "0x4883FA00740C8B41148902B800000000EB05B803400080C3"
Global Const $bDMO_MBGetBufferAndLengthASM64 = "0x4883FA007407488B41184889024983F800740D8B4110418900B800000000EB10B8000000004883FA007505B803400080C3"

#ASM _ASM_QueryInterface32
#	use32
#	push ebx
#	mov eax, [esp+8] ;pSelf
#	mov ebx, [esp+16] ;pOBJ
#	mov [ebx], eax; pObj = pSelf
#	add dword[eax+4], 1 ;AddRef
#	mov eax, 0
#	pop ebx
#	ret 12
#ASMEND

#ASM _ASM_QueryInterface64
#	use64
#	mov [r8], rcx; pObj = pSelf
#	add dword[rcx+8], 1 ;AddRef
#	mov eax, 0
#	ret
#ASMEND

#ASM _ASM_AddRef32
#	use32
#	mov eax, [esp+4] ;pSelf
#	add dword[eax+4], 1 ;AddRef
#	mov eax, [eax+4] ;Return RefCnt
#	ret 4
#ASMEND

#ASM _ASM_AddRef64
#	use64
#	add dword[rcx+8], 1 ;AddRef
#	mov eax, [rcx+8] ;Return RefCnt
#	ret
#ASMEND

#ASM _ASM_Release32
#	use32
#	mov eax, [esp+4] ;pSelf
#	sub dword[eax+4], 1 ;AddRef
#	cmp dword[eax+4], 0
#	jg _Ret
#		mov dword[eax+8], 1 ;IsFree = True
#	_Ret:
#		mov eax, [eax+4] ;Return RefCnt
#	ret 4
#ASMEND

#ASM _ASM_Release64
#	use64
#	sub dword[rcx+8], 1 ;AddRef
#	cmp dword[rcx+8], 0
#	jg _Ret
#		mov dword[rcx+12], 1 ;IsFree = True
#	_Ret:
#		mov eax, [rcx+8] ;Return RefCnt
#	ret
#ASMEND

#ASM _ASM_SetLength32
#	use32
#	push ebx
#	mov eax, [esp+8] ;pSelf
#	mov ebx, [esp+12] ;iLength
#	mov [eax+12], ebx ;SetLength
#	mov eax, 0 ;Return S_OK
#	pop ebx
#	ret 8
#ASMEND

#ASM _ASM_SetLength64
#	use64
#	mov [rcx+16], edx ;iLength
#	mov eax, 0 ;Return S_OK
#	ret
#ASMEND

#ASM _ASM_GetMaxLength32
#	use32
#	push ebx
#	push edx
#	mov eax, [esp+12] ;pSelf
#	mov ebx, [esp+16] ;pMax [out]
#	cmp ebx, 0
#	jz _Err
#		mov edx, [eax+16] ;edx = iSize
#		mov [ebx], edx; pMax = iSize
#		mov eax, 0
#		jmp _Ret
#	_Err:
#		mov eax, 0x80004003 ;E_POINTER
#	_Ret:
#	pop edx
#	pop ebx
#	ret 8
#ASMEND

#ASM _ASM_GetMaxLength64
#	use64
#	cmp rdx, 0
#	jz _Err
#		mov eax, [rcx+20]
#		mov [rdx], eax
#		mov eax, 0 ;Return S_OK
#		jmp _Ret
#	_Err:
#		mov eax, 0x80004003 ;E_POINTER
#	_Ret:
#	ret
#ASMEND

#ASM _ASM_GetBufferAndLength32
#	use32
#	push ebx
#	push ecx
#	push edx
#	mov eax, [esp+16] ;pSelf
#	mov ebx, [esp+20] ;pBuf [out]
#	mov ecx, [esp+24] ;pLen [out]

#	cmp ebx, 0
#	jz _Len
#		mov edx, [eax+20] ;edx = pBuffer
#		mov [ebx], edx; pBuf = pBuffer

#	_Len:
#	cmp ecx, 0
#	jz _Err
#		mov edx, [eax+12] ;edx = iLength
#		mov [ecx], edx; pLen = iLength
#		mov eax, 0 ;Return S_OK
#		jmp _Ret

#	_Err:
#		mov eax, 0 ;= S_OK
#		cmp ebx, 0
#		jnz _Ret
#		mov eax, 0x80004003 ;E_POINTER

#	_Ret:
#	pop edx
#	pop ecx
#	pop ebx
#	ret 12
#ASMEND

#ASM _ASM_GetBufferAndLength64
#	use64
#	cmp rdx, 0
#	jz _Len
#		mov rax, [rcx+24]
#		mov [rdx], rax

#	_Len:
#	cmp r8, 0
#	jz _Err
#		mov eax, [rcx+16]
#		mov [r8], eax
#		mov eax, 0 ;S_OK
#		jmp _Ret

#	_Err:
#		mov eax, 0 ;= S_OK
#		cmp rdx, 0
#		jnz _Ret
#		mov eax, 0x80004003 ;E_POINTER

#	_Ret:
#	ret
#ASMEND

; ###############################################################################################################################
; # DMO_GUID
; ###############################################################################################################################
Global Const $sGUID_NULL                = "{00000000-0000-0000-0000-000000000000}"

Global Const $sCLSID_DivxDecompressorCF = "{82CCD3E0-F71A-11D0-9FE5-00609778AAAA}"
Global Const $sCLSID_IV50_Decoder       = "{30355649-0000-0010-8000-00AA00389B71}"
Global Const $sCLSID_MemoryAllocator    = "{1E651CC0-B199-11D0-8212-00C04FC32C45}"
Global Const $sCLSID_CMP3DecMediaObject = "{BBEEA841-0A63-4F52-A7AB-A9B3A84ED38A}"

Global Const $sIID_IDivxFilterInterface = "{D132EE97-3E38-4030-8B17-59163B30A1F5}"
Global Const $sIID_IBaseFilter          = "{56A86895-0AD4-11CE-B03A-0020AF0BA770}"
Global Const $sIID_IEnumPins            = "{56A86892-0AD4-11CE-B03A-0020AF0BA770}"
Global Const $sIID_IEnumMediaTypes      = "{89C31040-846B-11CE-97D3-00AA0055595A}"
Global Const $sIID_IMemInputPin         = "{56A8689D-0AD4-11CE-B03A-0020AF0BA770}"
Global Const $sIID_IMemAllocator        = "{56A8689C-0AD4-11CE-B03A-0020AF0BA770}"
Global Const $sIID_IMediaSample         = "{56A8689A-0AD4-11CE-B03A-0020AF0BA770}"

Global Const $sMEDIATYPE_Audio         = "{73647561-0000-0010-8000-00AA00389B71}"
Global Const $sMEDIASUBTYPE_PCM        = "{00000001-0000-0010-8000-00AA00389B71}"
Global Const $sMEDIASUBTYPE_IEEE_FLOAT = "{00000003-0000-0010-8000-00AA00389B71}"
Global Const $sMEDIASUBTYPE_MP3        = "{00000055-0000-0010-8000-00AA00389B71}"

Global Const $sFORMAT_WaveFormatEx = "{05589F81-C356-11CE-BF01-00AA0055595A}"

Global Const $sDMOCATEGORY_AUDIO_DECODER        = "{57F2DB8B-E6BB-4513-9D43-DCD2A6593125}"
Global Const $sDMOCATEGORY_AUDIO_ENCODER        = "{33D9A761-90C8-11D0-BD43-00A0C911CE86}"
Global Const $sDMOCATEGORY_AUDIO_EFFECT         = "{F3602B3F-0592-48DF-A4CD-674721E7EBEB}"
Global Const $sDMOCATEGORY_VIDEO_EFFECT         = "{D990EE14-776C-4723-BE46-3DA2F56F10B9}"
Global Const $sDMOCATEGORY_AUDIO_CAPTURE_EFFECT = "{F665AABA-3E09-4920-AA5F-219811148F09}"
Global Const $sDMOCATEGORY_ACOUSTIC_ECHO_CANCEL = "{BF963D80-C559-11D0-8A2B-00A0C9255AC1}"
Global Const $sDMOCATEGORY_AUDIO_NOISE_SUPPRESS = "{E07F903F-62FD-4E60-8CDD-DEA7236665B5}"
Global Const $sDMOCATEGORY_AGC                  = "{E88C9BA0-C557-11D0-8A2B-00A0C9255AC1}"

Global Const $sIID_IMediaBuffer                 = "{59EFF8B9-938C-4A26-82F2-95CB84CDC837}"
Global Const $sIID_IMediaObject                 = "{D8AD0F58-5494-4102-97C5-EC798E59BCf4}"
Global Const $sIID_IEnumDMO                     = "{2C3CD98A-2BFA-4A53-9C27-5249BA64BA0F}"
Global Const $sIID_IMediaObjectInPlace          = "{651B9AD0-0fC7-4AA9-9538-D89931010741}"
Global Const $sIID_IDMOQualityControl           = "{65ABEA96-CF36-453F-AF8A-705E98F16260}"

Global Const $sIID_IMediaParamInfo = "{6d6cbb60-a223-44aa-842f-a2f06750be6d}"
Global Const $sIID_IMediaParams    = "{6d6cbb61-a223-44aa-842f-a2f06750be6e}"

Global Const $sGUID_TIME_REFERENCE = "{93ad712b-daa0-4ffe-bc81-b0ce500fcdd9}"
Global Const $sGUID_TIME_MUSIC     = "{0574c49d-5b04-4b15-a542-ae282030117b}"
Global Const $sGUID_TIME_SAMPLES   = "{a8593d05-0c43-4984-9a63-97af9e02c4c0}"

; ###############################################################################################################################
; # DMO_InterfaceDescription
; ###############################################################################################################################
Global Const $tagIMediaBuffer = "SetLength hresult(uint);" & _
		"GetMaxLength hresult(uint*);" & _
		"GetBufferAndLength hresult(ptr*;uint*);"

Global Const $tagIMediaObject = "GetStreamCount hresult(uint*;uint*);" & _
		"GetInputStreamInfo hresult(uint;uint*);" & _
		"GetOutputStreamInfo hresult(uint;uint*);" & _
		"GetInputType hresult(uint;uint;struct*);" & _
		"GetOutputType hresult(uint;uint;struct*);" & _
		"SetInputType hresult(uint;struct*;uint);" & _
		"SetOutputType hresult(uint;struct*;uint);" & _
		"GetInputCurrentType hresult(uint;struct*);" & _
		"GetOutputCurrentType hresult(uint;struct*);" & _
		"GetInputSizeInfo hresult(uint;uint*;uint*;uint*);" & _
		"GetOutputSizeInfo hresult(uint;uint*;uint*);" & _
		"GetInputMaxLatency hresult(uint;int64*);" & _
		"SetInputMaxLatency hresult(uint;int64);" & _
		"Flush hresult();" & _
		"Discontinuity hresult(uint);" & _
		"AllocateStreamingResources hresult();" & _
		"FreeStreamingResources hresult();" & _
		"GetInputStatus hresult(uint;uint*);" & _
		"ProcessInput hresult(uint;struct*;uint;int64;int64);" & _
		"ProcessOutput hresult(uint;uint;struct*;uint*);" & _
		"Lock hresult(int);"

Global Const $tagIEnumDMO = "Next hresult(uint;struct*;ptr*;uint*);" & _
		"Skip hresult(uint);" & _
		"Reset hresult();" & _
		"Clone hresult(ptr*);"

Global Const $tagIMediaObjectInPlace = "Process hresult(uint;struct*;int64;uint);" & _
        "Clone hresult(ptr*);" & _
        "GetLatency hresult(int64*);"

Global Const $tagIDMOQualityControl = "SetNow hresult(int64);" & _
		"SetStatus hresult(uint);" & _
		"GetStatus hresult(uint*);"

Global Const $tagIMediaParamInfo = "GetParamCount hresult(uint*);" & _
        "GetParamInfo hresult(uint;struct*);" & _
        "GetParamText hresult(uint;wstr*);" & _
        "GetNumTimeFormats hresult(uint*);" & _
        "GetSupportedTimeFormat hresult(uint;struct*);" & _
        "GetCurrentTimeFormat hresult(struct*;uint*);"

Global Const $tagIMediaParams = "GetParam hresult(uint;float*);" & _
        "SetParam hresult(uint;float);" & _
        "AddEnvelope hresult(uint;uint;struct*);" & _
        "FlushEnvelope hresult(uint;int64;int64);" & _
        "SetTimeFormat hresult(struct*;uint);"