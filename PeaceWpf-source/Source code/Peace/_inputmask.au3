#include-once
; #INDEX# ============================================================================================================
; Title .........: _Inputmask
; Version........: 1.0.0.5
; AutoIt Version : 3.3 +
; Language ......: English
; Description ...: Create input mask for GUI input controls
;				   Optimizes input validations dynamicaly
;				   (Almost) eliminate the need for post input validation
; Remarks .......: Windows message handler required: WM_COMMAND
;                  If the script already has WM_COMMAND handler then only set
;                    unregistered messages in _Inputmask_MsgRegister and call the relevant_Inputmask_WM_#####_Handler
;                    from within the existing handler
; Author ........: GreenCan
; Credits .......: Melba23: ideas picked from his validation examples, avoid conflicts with GUIRegisterMsg with WM_COMMAND
;                  Zedna: code optimization
; ====================================================================================================================
#include <EditConstants.au3>
#include <Misc.au3>
#include <GuiEdit.au3>

#region Constants
Global Const $iIM_INTEGER = 1 ; Input mask for INTEGERS only (..., -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, ...)
Global Const $iIM_POSITIVE_INTEGER = 2 ; Input mask for positive INTEGERS only (0, 1, 2, 3, 4, 5, ...)
Global Const $iIM_REAL = 3 ; Input mask for REAL numbers (..., -123.456, 0, 123.456, ...)
Global Const $iIM_POSITIVE_REAL = 4 ; Input mask for positive REAL numbers only (0, 23.562389, 123.456, ...)
Global Const $iIM_ALPHANUMERIC = 5 ; Input mask for Alphanumeric characters only (A-Za-z0-9)
Global Const $iIM_ALPHA = 6 ; Input mask for Alphabetic characters only (A-Za-z)
#endregion Constants

#region Global variables
Global $aInputMask[1][1], $iInputControls_count ; required Gloabal variables
#endregion Global variables

; #CURRENT# ==========================================================================================================
; _InputMask:                		   Windows message handler for WM_COMMAND
; _Inputmask_init:					   Initialize array for Input masks
; _Inputmask_close:					   Memory cleanup for Input masks
; _Inputmask_MsgRegister:			   Registers or unregisters Windows messages required for the UDF
; _Inputmask_add:					   Add a input mask for an Input control
; _Inputmask_Example:				   Example usage of the UDF
; ====================================================================================================================

; #INTERNAL_USE_ONLY#=================================================================================================
; none
; ====================================================================================================================
Func _Iif($Expression,$ValueTrue,$ValueFalse)
	If $Expression Then
		Return $ValueTrue
	Else
		Return $ValueFalse
	EndIf
EndFunc


; #FUNCTION# ====================================================================================================================
; Name...........: _InputMask
; Description....: Windows message handler for WM_COMMAND
; Syntax.........: _InputMask($hWnd, $iMsg, $wParam, $lParam)
; Parameters.....: As required by GUIRegisterMsg()
;				   $hWnd   - The Window handle of the GUI in which the message appears.
;				   $iMsg   - The Windows  message ID.
;				   $wParam - The first message parameter as hex value.
;				   $lParam - The second message parameter as hex value.
; Return values .: none
; Author.........: GreenCan
; Modified.......:
; Remarks........: The function will handle the content of $aInputMask array elements
;					$aInputMask[$i][0]. $iControlID (of GUICtrlCreateInput)
;					$aInputMask[$i][1]. $sRegExpMask (empty = not applicable)
;					$aInputMask[$i][2]. $iStringMaxLen (0 = no maximum)
;					$aInputMask[$i][3]. $sStringCase (empty = nothing, U = uppercase, L  lowercase, S = sentence cap, I = Initcap)
;					$aInputMask[$i][4]. $iPrecision (0 = no maximum) for number
;					$aInputMask[$i][5]. $iDecimal (0 = integer, >0 = Real number)
; Related........:
; Link...........:
; Example........: Yes
; ===============================================================================================================================
Func _InputMask($hWnd, $iMsg, $wParam, $lParam)

	If BitShift($wParam,16) = $EN_CHANGE Then
		Local $iPrecision, $iDecimal, $Read_Input, $Point1, $Point2, $Keep_Input, $aLocation
		Local $iIDFrom = BitAND($wParam, 0xFFFF);LoWord
		  For $i = 0 To UBound($aInputMask, 1) - 1
			If $iIDFrom = $aInputMask[$i][0] Then
				$Read_Input = GUICtrlRead($aInputMask[$i][0])
				$Keep_Input = $Read_Input
				$aLocation = _GUICtrlEdit_GetSel($aInputMask[$i][0]) ; cursor location

				; Part 1 - Input Mask
				Select

					Case $aInputMask[$i][4] > 0 ; number (including - and .)
						If StringLen($Read_Input) - StringInStr($Read_Input, "-") - _Iif(StringInStr($Read_Input, "."), 1, 0) > $aInputMask[$i][4] Then ; Precision
							$Read_Input = StringLeft($Read_Input, $aLocation[0] - 1) & StringRight($Read_Input,  $aInputMask[$i][4] + StringInStr($Read_Input, "-") + _Iif(StringInStr($Read_Input, "."), 1, 0) - $aLocation[0] + 1)
							$aLocation[0] -= 1
							$aLocation[1] -= 1
						ElseIf StringRegExp($Read_Input, $aInputMask[$i][1]) Then
							$Read_Input = StringRegExpReplace($Read_Input, $aInputMask[$i][1], '\1')
							$aLocation[0] -= 1
							$aLocation[1] -= 1
						EndIf
						$Point1 = StringInStr($Read_Input, ".", 0)
						$Point2 = StringInStr($Read_Input, ".", 0, 2)
						If $Point2 <> 0 Then
							$Read_Input = StringLeft($Read_Input, $Point2 - 1)
						EndIf
						If $Point1 <> 0 Then
							$Read_Input = StringLeft($Read_Input, $Point1 + $aInputMask[$i][5])
						EndIf
					Case $aInputMask[$i][2] > 0 And StringLen($Read_Input) > $aInputMask[$i][2] ; $iStringMaxLen
						$Read_Input = StringLeft($Read_Input, $aLocation[0] - 1) & StringRight($Read_Input, $aInputMask[$i][2] - $aLocation[0] + 1)
						$aLocation[0] -= 1
						$aLocation[1] -= 1
					Case $aInputMask[$i][1] <> "" And StringRegExp($Read_Input, $aInputMask[$i][1]) ; $sRegExpMask
						$Read_Input = StringRegExpReplace($Read_Input, $aInputMask[$i][1], '\1')
				EndSelect

				; Part 2 - String Case control
				If StringUpper($aInputMask[$i][3]) = "U" Then
					$Read_Input = StringUpper($Read_Input)
				ElseIf StringUpper($aInputMask[$i][3]) = "L" Then
					$Read_Input = StringLower($Read_Input)
				ElseIf StringUpper($aInputMask[$i][3]) = "S" Then
					$Read_Input = StringUpper(StringLeft($Read_Input,1)) & StringLower(StringTrimLeft($Read_Input,1))
				ElseIf StringUpper($aInputMask[$i][3]) = "I" Then
					$Read_Input = StringUpper(StringLeft($Read_Input,1)) & StringLower(StringTrimLeft($Read_Input,1))
					Local $ii = 1
					While 1
						$ii = StringInStr($Read_Input, " ", 0, 1, $ii)
						If $ii > 0 And $ii < StringLen($Read_Input) Then
							$ii += 1
							$Read_Input = StringLeft($Read_Input, $ii-1) & StringUpper(StringMid($Read_Input, $ii, 1)) & StringTrimLeft($Read_Input, $ii)
						Else
							ExitLoop
						EndIf
					WEnd
				EndIf

				; set the corrected control only if changed
				If Not ($Keep_Input == $Read_Input) Then
					GUICtrlSetData($aInputMask[$i][0], $Read_Input)
					_GUICtrlEdit_SetSel($aInputMask[$i][0], $aLocation[0], $aLocation[1])
				EndIf
			EndIf
		Next
	EndIf

	Return
EndFunc ;==>_InputMask

; #FUNCTION# ====================================================================================================================
; Name...........: _Inputmask_init
; Description....: Initialize array for Input masks
; Syntax.........: _Inputmask_init([$iInputControls = 1])
; Parameters.....: $iInputControls - Optional: Number of Input controls requiring an input mask
; Return values .: none
; Author.........: GreenCan
; Modified.......:
; Remarks........: $aInputMask and $iInputControls_count require to be decalred Global prior to call _Inputmask_init()
; Related........:
; Link...........:
; Example........: Yes
; ===============================================================================================================================
Func _Inputmask_init($iInputControls = 10)
	ReDim $aInputMask[$iInputControls][6]
	$iInputControls_count = 0
	Return
EndFunc   ;==>_Inputmask_init

; #FUNCTION# ====================================================================================================================
; Name...........: _Inputmask_close
; Description....: Memory cleanup for Input masks
; Syntax.........: _Inputmask_close()
; Parameters.....: none
; Return values .: none
; Author.........: GreenCan
; Modified.......:
; Remarks........:
; Related........:
; Link...........:
; Example........: Yes
; ===============================================================================================================================
Func _Inputmask_close()
	ReDim $aInputMask[1][1]
	$aInputMask[0][0] = ""
	Return
EndFunc   ;==>_Inputmask_close

; #FUNCTION# =========================================================================================================
; Name...........: _Inputmask_MsgRegister
; Description ...: Registers Windows messages required for the UDF
; Syntax.........: _Inputmask_MsgRegister([$bCOMMAND = True])
; Parameters ....: $bCOMMAND - True = Register WM_COMMAND message
; Requirement(s).: v3.3 +
; Return values .: None
; Author ........: Melba23
; Modified ......: GreenCan
; Remarks .......: If other handlers already registered, then call the relevant handler function from within that handler
; Example........: Yes
;=====================================================================================================================
Func _Inputmask_MsgRegister($bCOMMAND = True)
	; Register required messages
	If $bCOMMAND Then
		GUIRegisterMsg(0x0111, "_InputMask") ; $WM_COMMAND
	Else
		GUIRegisterMsg(0x0111, "") ; $WM_COMMAND
	EndIf
EndFunc   ;==>_Inputmask_MsgRegister

; #FUNCTION# ====================================================================================================================
; Name...........: _Inputmask_add
; Description....: Add a input mask for an Input control
; Syntax.........: _Inputmask_add($iControlID[, $sRegExpMask = ""[, $iStringMaxLen = 0[, $sStringCase = ""[, $iPrecision = 0[, $iDecimal = 0]]]]])
; Parameters.....: $iControlID    - Identifier of the Input Control
;                  $sRegExpMask   - Optional: StringRegExp pattern, to check if the input fits the regular expression pattern.
;									$sRegExpMask can be either a Preset mask(see Preset Input masks constants in Remarks hereunder)
;								    either a string containing the StringRegExp pattern if applicable. If "" StringRegExp not applicable. Default = ""
;                  $iStringMaxLen - Optional: Maximum length of a string in the current Input control. 0 = no maximum. Default = 0
;                  $sStringCase   - Optional: Case awareness of the current Input control. 0 = no maximum. Default = ""
;                                      	  		"" = nothing
;                                         		U  = uppercase
;                                         		L  = lowercase
;                                         		S  = sentence cap
;                                         		I  = Initcap (Capitalizes every first letter of a word)
;                  $iPrecision    - Optional: Precision value of a number. Default = 0
;                  $iDecimal      - Optional: Scale value of a number. Default = 0
; Return values .: Success - Return 1 and @error 0
;				   Failure - Returns 0 and @error
;                  |1 - $iControlID is not a number
;                  |2 - invalid $sRegExpMask preset
;                  |3 - $iStringMaxLen is not a number
;                  |4 - $sStringCase is not valid
;                  |5 - $iPrecision is not a number
;                  |6 - $iDecimal is not a number or $iDecimal to large versus $iPrecision
; Author.........: GreenCan
; Modified.......:
; Remarks........: $aInputMask and $iInputControls_count require to be decalred Global prior to call _Inputmask_init()
;				   Any error will result in the input mask to be disregarded for the current Input Control
;
;				   Preset Input masks constants:
;					 Const $iIM_INTEGER = 1 ; Input mask for INTEGERS only (..., -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, ...)
;					 Const $iIM_POSITIVE_INTEGER = 2 ; Input mask for positive INTEGERS only (0, 1, 2, 3, 4, 5, ...)
;					 Const $iIM_REAL = 3 ; Input mask for REAL numbers (..., -123.456, 0, 123.456, ...)
;					 Const $iIM_POSITIVE_REAL = 4 ; Input mask for positive REAL numbers only (0, 23.562389, 123.456, ...)
;					 Const $iIM_ALPHANUMERIC = 5 ; Input mask for Alphanumeric characters only (A-Za-z0-9)
;					 Const $iIM_ALPHA = 6 ; Input mask for Alphabetic characters only (A-Za-z)
; Related........:
; Link...........:
; Example........: Yes
; ===============================================================================================================================
Func _Inputmask_add($iControlID, $sRegExpMask = "", $iStringMaxLen = 0, $sStringCase = "", $iPrecision = 0, $iDecimal = 0)
	$iInputControls_count += 1
	If Not IsNumber($iControlID) Then Return SetError(1, 0, 0) ; no error checking on the validity of the Input Control Identifier !
	If IsNumber($sRegExpMask) And $sRegExpMask < 1 Or $sRegExpMask > 6 Then Return SetError(2, 0, 0)
	If Not IsNumber($iStringMaxLen) Then Return SetError(3, 0, 0)
	If StringInStr("ULSI", $sStringCase) = 0 And $sStringCase <> "" Then Return SetError(4, 0, 0)
	If Not IsNumber($iPrecision) Then Return SetError(5, 0, 0)
	If Not IsNumber($iDecimal) Or ($iDecimal > 0 And $iDecimal >= $iPrecision) Then Return SetError(6, 0, 0)

	If $sRegExpMask = $iIM_INTEGER Then $sRegExpMask = "[^\d-]|([{0-9,1}^\A-])[^\d.]"
	If $sRegExpMask = $iIM_POSITIVE_INTEGER Then $sRegExpMask = "[\D]|([{0-9,1}^\A-])"
	If $sRegExpMask = $iIM_REAL Then $sRegExpMask = "[^\d.-]|([{0-9,1}^\A-])[^\d.]"
	If $sRegExpMask = $iIM_POSITIVE_REAL Then $sRegExpMask = "[^\d.]|([{0-9,1}^\A-])[^\d.]"
	If $sRegExpMask = $iIM_ALPHANUMERIC Then $sRegExpMask = "[^A-Za-z0-9]"
	If $sRegExpMask = $iIM_ALPHA Then $sRegExpMask = "[^A-Za-z]"


	$aInputMask[$iInputControls_count-1][0] = $iControlID
	$aInputMask[$iInputControls_count-1][1] = $sRegExpMask
	$aInputMask[$iInputControls_count-1][2] = $iStringMaxLen
	$aInputMask[$iInputControls_count-1][3] = $sStringCase
	$aInputMask[$iInputControls_count-1][4] = $iPrecision
	$aInputMask[$iInputControls_count-1][5] = $iDecimal
	Return 1
EndFunc   ;==>_Inputmask_add

#region Example
#cs
#include <GUIConstantsEx.au3>
#include <_inputmask.au3>
#include <Array.au3>

_Inputmask_Example()

; #FUNCTION# ====================================================================================================================
; Name...........: _Inputmask_Example
; Description....: Example usage of the UDF
; Syntax.........: _Inputmask_Example()
; Parameters.....: none
; Return values .: none
; Author.........: GreenCan
; Modified.......:
; Remarks........: Before using _Inputmask_add() don't forget to initialize the UDF with _Inputmask_init(x)
;				   where x is the number of input masks that you will create
; Related........:
; Link...........:
; Example........: Yes
; ===============================================================================================================================
Func _Inputmask_Example()
	Local $GUI, $sLabel_Mask
	Local $Input_1, $Input_2, $Input_3, $Input_4, $Input_5, $Input_6, $Input_7, $Input_8, $Input_9, $Input_10, $Input_11, $Input_12, $Input_13
	Local $iInputs = 15

	_Inputmask_init($iInputs) ; Initialize InputMasks for Inputs controls
	_Inputmask_MsgRegister() ; Register for _InputMask

	$GUI = GUICreate("InputMask Example",330,600)

	$sLabel_Mask = "1. Char(4)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 20, 200, 15)
	$Input_1 = GUICtrlCreateInput("",210,20,110,20)
	_Inputmask_add($Input_1, "", 4)

	$sLabel_Mask = "2. Integer(6)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 50, 200, 15)
	$Input_2 = GUICtrlCreateInput("",210,50,110,20);, $ES_NUMBER) ; I don't use $ES_NUMBER
	_Inputmask_add($Input_2, $iIM_POSITIVE_INTEGER, 0, "", 6) ; using a preset Mask

	$sLabel_Mask = "3. Neg Integer(5)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 80, 200, 15)
	$Input_3 = GUICtrlCreateInput("",210,80,110,20)
	_Inputmask_add($Input_3, $iIM_INTEGER, 0, "", 5)

	$sLabel_Mask = "4. Decimal(8,2)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 110, 200, 15)
	$Input_4 = GUICtrlCreateInput("",210,110,110,20)
	_Inputmask_add($Input_4, $iIM_POSITIVE_REAL, 0, "", 8, 2)

	$sLabel_Mask = "5. Neg Decimal(10,4)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 140, 200, 15)
	$Input_5 = GUICtrlCreateInput("",210,140,110,20)
	_Inputmask_add($Input_5, $iIM_REAL, 0, "", 10, 4)

	$sLabel_Mask = "6. Alpha only"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 170, 200, 15)
	$Input_6 = GUICtrlCreateInput("",210,170,110,20)
	_Inputmask_add($Input_6, $iIM_ALPHA)

	$sLabel_Mask = "7. Alpha Ucase only(5)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 200, 200, 15)
	$Input_7 = GUICtrlCreateInput("",210,200,110,20)
	_Inputmask_add($Input_7, $iIM_ALPHA, 5, "U")

	$sLabel_Mask = "8. Alpha Lowercase only"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 230, 200, 15)
	$Input_8 = GUICtrlCreateInput("",210,230,110,20)
	_Inputmask_add($Input_8, $iIM_ALPHA, 0, "L")

	$sLabel_Mask = "9. Char(3) Uppercase"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 260, 200, 15)
	$Input_9 = GUICtrlCreateInput("",210,260,110,20)
	_Inputmask_add($Input_9, "", 3, "U")

	$sLabel_Mask = "10. Sentence Cap Alpha only (12)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 290, 200, 15)
	$Input_10 = GUICtrlCreateInput("",210,290,110,20)
	_Inputmask_add($Input_10, $iIM_ALPHA, 12, "S")
;~	 _Inputmask_add($Input_10, $iIM_ALPHA, 12, "x") ; simulate an error (x does not exist) in _Inputmask_add, if the mask is invalid, the mask will be disregarded in the input

	$sLabel_Mask = "11. InitCap Alpha+Space+Dash only (20)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 320, 200, 15)
	$Input_11 = GUICtrlCreateInput("",210,320,110,20)
	_Inputmask_add($Input_11, "[^A-Za-z :space: -]", 20, "I") ; using a StringRegExp pattern

	$sLabel_Mask = "12. Alphanumeric(50)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 350, 200, 15)
	$Input_12 = GUICtrlCreateInput("",210,350,110,20)
	_Inputmask_add($Input_12, $iIM_ALPHANUMERIC, 50)

	$sLabel_Mask = "13. Alphanumeric+space(50)"
	GuiCtrlCreateLabel($sLabel_Mask, 10, 380, 200, 15)
	$Input_13 = GUICtrlCreateInput("",210,380,110,20)
	_Inputmask_add($Input_13, "[^A-Za-z0-9  :space:]", 50)

	GUISetState()

;~ 	_ArrayDisplay($aInputMask) ; check the array here

	While GUIGetMsg() <> $GUI_EVENT_CLOSE
	WEnd

	_Inputmask_MsgRegister(False) ; Unregister for _InputMask
	_Inputmask_close() ; Memory cleanup for Input masks

	Return
EndFunc ;==>_Inputmask_Example
#ce
#endregion Example