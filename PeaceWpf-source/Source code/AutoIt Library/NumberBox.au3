#include-once
#include "Logics and Math.au3"
#include <WindowsConstants.au3>
#include <GUIConstantsEx.au3>
#include <GuiEdit.au3>
; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.28
; Description....: Number Input Box Functions
; Author(s)......: Peter Verbeek
; Version history: 1.28		added: $NB_NOREGISTER to indicate that own WM_COMMAND handler is being used instead of number box handler
;							added: _NumberBoxUnregister() to unregister number input boxes message handler and/or reset variables
;							added: _NumberBoxPushCounter() to push the number input boxes counter to the position stack
;							added: _NumberBoxPopCounter() to pop the number input boxes counter from the position stack removing the number input boxes pushed
;				   1.27		added: Number input box library for decimal, binary and hexadecimal numbers with examples in GUIExamples()
;							added: _NumberBoxReset() to reset global number input boxes array
;							added: _NumberBoxRegister() to (un)register number input boxes message handler and/or reset variables
;							added: _GUICtrlNumberBox_Create() to create a number input box control
;							added: _NumberBoxAdd() to add a number format to an input box, making it a number input box
;							added: _NumberBoxRemove() to remove number input boxes from global array
;							added: _NumberBoxCount() to return number of number input boxes
;							added: _NumberBoxHandler() handler to process a number input box
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
; _NumberBoxReset
; _NumberBoxRegister
; _NumberBoxUnregister
; _GUICtrlNumberBox_Create
; _NumberBoxAdd
; _NumberBoxRemove
; _NumberBoxPushCounter
; _NumberBoxPopCounter
; _NumberBoxCount
; _NumberBoxHandler
; ===============================================================================================================================

; List of 10 functions
;	_NumberBoxReset						Control		Resets global number input boxes array
;	_NumberBoxRegister					Control		Registers or unregisters number input boxes message handler and/or reset variables
;	_NumberBoxUnregister				Control		Unregisters number input boxes message handler and/or reset variables
;	_GUICtrlNumberBox_Create			Control		Creates a number input box control
;	_NumberBoxAdd						Control		Adds a number format to an input box, making it a number input box
;	_NumberBoxRemove					Control		Removes number input boxes from global array
;	_NumberBoxPushCounter				Control		Pushes the number input boxes counter to the position stack
;	_NumberBoxPopCounter				Control		Pops the number input boxes counter from the position stack removing the number input boxes pushed
;	_NumberBoxCount						Control		Returns number of number input boxes
;	_NumberBoxHandler					Control		Processes input in a number input box

Global Const $NB_REGISTER = 0, _			; register number box message handler
			 $NB_UNREGISTER = 1, _			; unregister and reset global number input box array
			 $NB_UNREGISTERNORESET = 2, _	; unregister but don't reset array
			 $NB_NOREGISTER = 3, _			; don't register number box message handler when WM_COMMAND handler of calling program is used
			 $NB_NORMALRUN = 0, _			; run AutoIt message handler only when it isn't a number input box and isn't the $EN_CHANGE message, ensuring normal processing of input boxes
			 $NB_ALWAYSRUN = 1, _			; always run AutoIt message handler
			 $NB_NORUN = 2, _				; don't run AutoIt message handler for every control
			 $NB_DECIMAL = 0, _				; decimal number input box
			 $NB_BINARY = 1, _				; binary number input box
			 $NB_HEXADECIMAL = 2, _			; hexadecimal number input box
			 $NB_HEXADECIMALUPPER = 3		; hexadecimal number input box, upper case
Global $_NumberBoxControls = [[-1,"","",0,0,0,0,0]],$_NumberBoxUnregister = True,$_NumberBoxReturn = $NB_NORMALRUN,$_NumberBoxCounterStack = [0]

;~ GUIExamples()	; uncomment to run examples of number input boxes

Func GUIExamples()
	Const $nMargin = 10,$nGUIWidth = 320,$nGUIHeight = 250,$OffsetX = 160,$nNumberBoxWidth = 60,$nNumberBoxHeight = 20,$nLabelOffsetY = 3
	Local $hGUI,$nOffSetY = $nMargin,$aMsg,$nNumberBox,$nBinaryBox,$nHexaBox

	$hGUI = GUICreate("Number input box examples",$nGUIWidth,$nGUIHeight)

	GUICtrlCreateLabel("Positive integer 3",$nMargin,$nOffSetY+$nLabelOffsetY)
	_GUICtrlNumberBox_Create("999",1,$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Format 999: Positive integer of length 3")
	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Positive/negative integer 2",$nMargin,$nOffSetY+$nLabelOffsetY)
	_GUICtrlNumberBox_Create("-99",-20,$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Format -99: Positive/negative integer of length 2")
	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Positive real 2.2",$nMargin,$nOffSetY+$nLabelOffsetY)
	_GUICtrlNumberBox_Create("99.99",8.3,$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Format 99.99: Positive real of length 2 before decimal point and length 2 after")
	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Positive/negative real 1.3",$nMargin,$nOffSetY+$nLabelOffsetY)
	_GUICtrlNumberBox_Create("-9.999",-3.141,$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Format -9.999: Positive/negative real of length 1 before decimal point and length 3 after")

	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Input box with added format",$nMargin,$nOffSetY+$nLabelOffsetY)
	$nNumberBox = GUICtrlCreateInput("123",$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	_NumberBoxAdd($nNumberBox,"999")
	GUICtrlSetTip(-1,"The 999 number format is added later on to this standard input box")
	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Maximum integer of 500",$nMargin,$nOffSetY+$nLabelOffsetY)
	_GUICtrlNumberBox_Create("500",100,$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Format 500: Maximum integer of 500")
	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Integer between -10 and 100",$nMargin,$nOffSetY+$nLabelOffsetY)
	_GUICtrlNumberBox_Create("100 -10",0,$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Format 100 -10: Integer between a minimum and a maximimum")

	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Maximum binary number 128",$nMargin,$nOffSetY+$nLabelOffsetY)
	$nBinaryBox = _GUICtrlNumberBox_Create("b10000000","01010101",$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Binary format b10000000: 8 bits, maximum of 128 decimal")
	$nOffSetY += $nNumberBoxHeight+5
	GUICtrlCreateLabel("Hexadecimal number",$nMargin,$nOffSetY+$nLabelOffsetY)
	$nHexaBox = _GUICtrlNumberBox_Create("h8000","0000",$nMargin+$OffsetX,$nOffSetY,$nNumberBoxWidth,$nNumberBoxHeight)
	GUICtrlSetTip(-1,"Hexadecimal format h8000: maximum of 8000 hexadecimal")

	_NumberBoxRegister()
	; when you have a WM_COMMAND handler of your own, use this:
	; _NumberBoxRegister($NB_NOREGISTER)
	; and put _NumberBoxHandler($hWnd,$iMsg,$wParam,$lParam) somewhere in your handler
	GUISetState(@SW_SHOW,$hGUI)
	While 1
		$aMsg = GUIGetMsg(1)
		Switch $aMsg[0]
			Case $GUI_EVENT_CLOSE
				ExitLoop
		EndSwitch
	WEnd
	_NumberBoxRegister($NB_UNREGISTER)
	GUIDelete($hGUI)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxReset
; Description....: Resets global array of number input boxes
; Syntax.........: _NumberBoxReset()
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxReset()
	Global $_NumberBoxControls = [[-1,"","",0,0,0,0,0]],$_NumberBoxUnregister = True,$_NumberBoxReturn = $NB_NORMALRUN,$_NumberBoxCounterStack = [0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxRegister
; Description....: Registers or unregisters number input boxes message handler and/or reset variables
; Syntax.........: _NumberBoxRegister($nRegister = $NB_REGISTER, $nReturn = $NB_NORMALRUN)
; Parameters.....: $nRegister				- Register action:
;											$NB_REGISTER = Register for WM_COMMAND
;											$NB_UNREGISTER = Unregister and reset global number input boxes array
;											$NB_UNREGISTERNORESET = Unregister but leave input boxes array as is
;											$NB_NOREGISTER = don't register number box message handler when WM_COMMAND handler of calling program is used
;				   $nReturn					- How to run AutoIt internal message handler
;			 								$NB_NORMALRUN = run AutoIt message handler only when it isn't a number input box and isn't the $EN_CHANGE message, ensuring normal processing of input boxes
;											$NB_ALWAYSRUN = always run AutoIt message handler
;											$NB_NORUN = don't run AutoIt message handler for every control
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxRegister($nRegister = $NB_REGISTER,$nReturn = $NB_NORMALRUN)
	If $nRegister = $NB_NOREGISTER Then
		$_NumberBoxReturn = $NB_NORUN	; calling program has own WM_COMMAND handler
		$_NumberBoxUnregister = False
	ElseIf $nRegister = $NB_REGISTER Then
		$_NumberBoxReturn = $nReturn
		GUIRegisterMsg($WM_COMMAND,"_NumberBoxHandler")
	Else
		If $_NumberBoxUnregister Then GUIRegisterMsg($WM_COMMAND,"")
		If $nRegister = $NB_UNREGISTER Then _NumberBoxReset()
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxUnregister
; Description....: Unregisters number input boxes message handler and/or reset variables
; Syntax.........: _NumberBoxRegister($nRegister = $NB_REGISTER, $nReturn = $NB_NORMALRUN)
; Parameters.....: None
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxUnregister()
	If $_NumberBoxUnregister Then GUIRegisterMsg($WM_COMMAND,"")
	_NumberBoxReset()
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _GUICtrlNumberBox_Create
; Description....: Creates an input box specific for numbers (decimal, binary or hexadecimal
; Syntax.........: _GUICtrlNumberBox_Create($sFormat, $nNumber, $nLeft, $nTop [, $nWidth [, $nHeight [, $nStyle = -1 [, $nExStyle = -1]]]])
; Parameters.....: $sFormat					- Format of number, length determines number of numbers
;											First part
;												9		= Positive integers
;												-9		= Positive and negative integers
;												9.9		= Positive reals
;												-9.9	= Positive and negative reals
;												b1		= Binary number
;												hF		= Hexadecimal number
;												HF		= Hexadecimal number, upper case forced
;											Second part number in format is lower bound (see examples)
; Examples.......: "999" -> positive integer of 3 digits
;				   "-99" -> positive/negative integer of 2 digits
;				   "99.99" -> positive real of 2 digits before and 2 digits after decimal point
;				   "-9999.9" -> positive/negative real of 4 digits before and 1 digit after decimal point
;				   "b1111111" -> 8 bit binary number
;				   "hffff" -> 4 byte hexadecimal number
;				   "Hffff" -> 4 byte hexadecimal number, upper case forced
;				   "500 -100" integer from -100 to 500
;				   "3.14 -3.14" real from -3.14 to 3.14
; Return values..: Control ID
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _GUICtrlNumberBox_Create($sFormat,$nNumber,$nLeft,$nTop,$nWidth = Default,$nHeight = Default,$nStyle = -1,$nExStyle = -1)
	Local $nControlID = GUICtrlCreateInput($nNumber,$nLeft,$nTop,$nWidth,$nHeight,$nStyle,$nExStyle)
	_NumberBoxAdd($nControlID,$sFormat)
	Return $nControlID
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxAdd
; Description....: Makes a number box from an input box by adding a number format
; Syntax.........: _NumberBoxAdd($nControlID, $sFormat)
; Parameters.....: $sFormat					- Format of number
; Examples.......: See GUICtrlCreateNumber()
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxAdd($nControlID,$sFormat)
	Local $nType = $NB_DECIMAL,$nDecimalPoint = StringInStr($sFormat,"."),$nDigits,$nFraction,$bPoint = False,$sMinimum,$sMaximum,$nCountDecimals = 0,$nCountFraction = 0, _
		  $sDigits = "+0123456789" & ($nDecimalPoint > 0 ? "." : "") & (StringInStr($sFormat,"-") > 0 ? "-" : "")

	If StringLeft($sFormat,1) = "b" or StringLeft($sFormat,1) = "h" Or StringLeft($sFormat,1) = "H" Then	; binary or hexadecimal
		Switch StringLeft($sFormat,1)
			Case "b"
				$nType = $NB_BINARY
				$sDigits = "01"
			Case "h"
				$nType = $NB_HEXADECIMAL
				$sDigits = "0123456789abcdef"
			Case "H"
				$nType = $NB_HEXADECIMALUPPER
				$sDigits = "0123456789abcdef"
		EndSwitch
		$sFormat = StringTrimLeft($sFormat,1)
		If StringInStr($sFormat," ") > 0 Then
			$sMinimum = StringTrimLeft($sFormat,StringInStr($sFormat," "))
			$sFormat = StringLeft($sFormat,StringInStr($sFormat," ")-1)
		Else
			$sMinimum = 0
		EndIf
		$nDigits = StringLen($sFormat)
		$sMaximum = $sFormat
	ElseIf StringInStr($sFormat," ") > 0 Then	; minimum number provided
		$sMinimum = StringTrimLeft($sFormat,StringInStr($sFormat," "))
		$sFormat = StringLeft($sFormat,StringInStr($sFormat," ")-1)
		$sMaximum = $sFormat
		$nDecimalPoint = StringInStr($sMaximum,".")
		If StringInStr($sMinimum,".") > $nDecimalPoint Then
			$nDigits = StringLen($nDecimalPoint > 0 ? StringLeft($sMinimum,$nDecimalPoint) : $sMinimum)
			$nFraction = $nDecimalPoint > 0 ? StringLen(StringTrimLeft($sMinimum,$nDecimalPoint)) : 0
		Else
			$nDigits = StringLen($nDecimalPoint > 0 ? StringLeft($sMaximum,$nDecimalPoint) : $sMaximum)
			$nFraction = $nDecimalPoint > 0 ? StringLen(StringTrimLeft($sMaximum,$nDecimalPoint)) : 0
		EndIf
	Else
		$sMaximum = StringReplace($sFormat,"-","")
		$sMinimum = StringInStr($sFormat,"-") > 0 ? $sFormat : 0
		$nDecimalPoint = StringInStr($sMaximum,".")
		$nDigits = StringLen($nDecimalPoint > 0 ? StringLeft($sMaximum,$nDecimalPoint) : $sMaximum)
		$nFraction = $nDecimalPoint > 0 ? StringLen(StringTrimLeft($sMaximum,$nDecimalPoint)) : 0
	EndIf
	If $_NumberBoxControls[0][0] <> -1 Then ReDim $_NumberBoxControls[UBound($_NumberBoxControls)+1][UBound($_NumberBoxControls,$UBOUND_COLUMNS)]
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][0] = $nControlID
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][1] = $sFormat
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][2] = $nType
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][3] = $sDigits
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][4] = $nDigits
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][5] = $nFraction
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][6] = Number($sMinimum)
	$_NumberBoxControls[UBound($_NumberBoxControls)-1][7] = $nType >= $NB_HEXADECIMAL ? Number("0x" & $sMaximum) : Number($sMaximum)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxRemove
; Description....: Removes last added number input boxes from global array
; Syntax.........: _NumberBoxRemove($nRemove)
; Parameters.....: $nRemove					- Number of number input boxes to remove
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxRemove($nRemove)
	ReDim $_NumberBoxControls[UBound($_NumberBoxControls)-$nRemove][UBound($_NumberBoxControls,$UBOUND_COLUMNS)]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxPushCounter
; Description....: Pushes the number input boxes counter to the position stack
; Syntax.........: _NumberBoxPushCounter()
; Parameters.....: None
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxPushCounter()
	ReDim $_NumberBoxCounterStack[UBound($_NumberBoxCounterStack)+1]
	$_NumberBoxCounterStack[UBound($_NumberBoxCounterStack)-1] = UBound($_NumberBoxControls)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxPopCounter
; Description....: Pops the number input boxes counter from the position stack removing the number input boxes pushed
; Syntax.........: _NumberBoxPopCounter()
; Parameters.....: None
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxPopCounter()
	If _NumberBoxCount() = 0 Then Return
	If UBound($_NumberBoxCounterStack) = 1 Then
		Global $_NumberBoxControls = [[-1,"","",0,0,0,0,0]]
		Return
	EndIf
	_NumberBoxRemove(_NumberBoxCount()-$_NumberBoxCounterStack[UBound($_NumberBoxCounterStack)-1])
	ReDim $_NumberBoxCounterStack[UBound($_NumberBoxCounterStack)-1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxCount
; Description....: Returns number of number input boxes
; Syntax.........: _NumberBoxCount()
; Parameters.....: None
; Return values..: > 0 = Number of number input boxes, 0 = no number input boxes yet added
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxCount()
	If  $_NumberBoxControls[0][0] = -1 Then Return 0
	Return UBound($_NumberBoxControls)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberBoxHandler
; Description....: When having your own WM_COMMAND handler put this function in it to handle the numberbox input
; Syntax.........: _NumberBoxHandler($hWnd,$iMsg,$wParam,$lParam)
; Parameters.....: The usual window message parameters
; Return values..: none
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberBoxHandler($hWnd,$iMsg,$wParam,$lParam)
	If BitShift($wParam,16) <> $EN_CHANGE Then
		If $_NumberBoxReturn = $NB_NORUN Then Return	; skip AutoIt internal message handler
		Return $GUI_RUNDEFMSG	; run AutoIt internal message handler
	EndIf

	Local $nControl = BitAND($wParam,0xFFFF)
	For $nNumberBoxControl = 0 To UBound($_NumberBoxControls)-1
		If $_NumberBoxControls[$nNumberBoxControl][0] = $nControl Then	; input control found
			Local $sNumber = GUICtrlRead($nControl),$aSelection = _GUICtrlEdit_GetSel($nControl),$sNewNumber = "",$bPoint = False,$nCountDigits = 0,$nCountFraction = 0
			For $nDigit = 1 To StringLen($sNumber)
				If StringInStr($_NumberBoxControls[$nNumberBoxControl][3],StringMid($sNumber,$nDigit,1)) > 0 Then	; allowed digit/character
					If $_NumberBoxControls[$nNumberBoxControl][2] >= $NB_BINARY Then ; process binary or hexadecimal number
						$nCountDigits += 1
						If $nCountDigits > $_NumberBoxControls[$nNumberBoxControl][4] Then ContinueLoop	; more digits than allowed
						If $_NumberBoxControls[$nNumberBoxControl][2] >= $NB_HEXADECIMAL Then
							If Number("0x" & $sNewNumber & StringMid($sNumber,$nDigit,1)) > $_NumberBoxControls[$nNumberBoxControl][7] Then ContinueLoop	; too large
						EndIf
						If $_NumberBoxControls[$nNumberBoxControl][2] = $NB_BINARY Then
							If _BitsToNumber($sNewNumber & StringMid($sNumber,$nDigit,1)) > _BitsToNumber($_NumberBoxControls[$nNumberBoxControl][7]) Then ContinueLoop	; too large
						EndIf
						$sNewNumber &= StringMid($sNumber,$nDigit,1)
						ContinueLoop
					EndIf
					If StringMid($sNumber,$nDigit,1) = "." Then
						If Not $bPoint Then						; allow only one decimal point
							$bPoint = True
							$sNewNumber &= "."
						EndIf
						ContinueLoop
					EndIf
					If StringMid($sNumber,$nDigit,1) = "-" Then	; allow for first character minus sign
						If StringLeft($sNewNumber,1) <> "-" Then $sNewNumber = "-" & $sNewNumber
						ContinueLoop
					EndIf
					If StringMid($sNumber,$nDigit,1) = "+" Then
						If StringLeft($sNumber,1) = "-" Then $sNewNumber = StringTrimLeft($sNewNumber,1)	; make positive if negative
						ContinueLoop
					EndIf
					If $bPoint Then
						$nCountFraction += 1
						If $nCountFraction > $_NumberBoxControls[$nNumberBoxControl][5] Then ContinueLoop	; more decimal fraction digits than allowed
					Else
						$nCountDigits += 1
						If $nCountDigits > $_NumberBoxControls[$nNumberBoxControl][4] Then ContinueLoop	; more digits than allowed
					EndIf
					If StringMid($sNumber,$nDigit,1) = "0" Or (Number($sNewNumber & StringMid($sNumber,$nDigit,1)) >= $_NumberBoxControls[$nNumberBoxControl][6] And Number($sNewNumber & StringMid($sNumber,$nDigit,1)) <= $_NumberBoxControls[$nNumberBoxControl][7]) Then
						$sNewNumber &= StringMid($sNumber,$nDigit,1)	; allowed digit/character
					EndIf
				EndIf
			Next
			Switch $_NumberBoxControls[$nNumberBoxControl][2]
				Case $NB_BINARY		; binary bits number
					$sNewNumber = _BitsToNumber($sNewNumber) < _BitsToNumber($_NumberBoxControls[$nNumberBoxControl][6]) ? $_NumberBoxControls[$nNumberBoxControl][6] : $sNewNumber
					$sNewNumber = _BitsToNumber($sNewNumber) > _BitsToNumber($_NumberBoxControls[$nNumberBoxControl][7]) ? $_NumberBoxControls[$nNumberBoxControl][7] : $sNewNumber
				Case $NB_HEXADECIMAL,$NB_HEXADECIMALUPPER	; hexadecimal number
					$sNewNumber = Number("0x" & $sNewNumber) < $_NumberBoxControls[$nNumberBoxControl][6] ? $_NumberBoxControls[$nNumberBoxControl][6] : $sNewNumber
					$sNewNumber = Number("0x" & $sNewNumber) > $_NumberBoxControls[$nNumberBoxControl][7] ? $_NumberBoxControls[$nNumberBoxControl][7] : $sNewNumber
				Case $NB_DECIMAL	; decimal number
					$sNewNumber = _Clamp($sNewNumber,$_NumberBoxControls[$nNumberBoxControl][6],$_NumberBoxControls[$nNumberBoxControl][7])
			EndSwitch
			GUICtrlSetData($nControl,$sNewNumber)
			_GUICtrlEdit_SetSel($nControl,$aSelection[0],$aSelection[1])
			If $_NumberBoxReturn = $NB_ALWAYSRUN Then Return $GUI_RUNDEFMSG	; run AutoIt internal message handler
			Return		; skip AutoIt internal message handler
		EndIf
	Next
	If $_NumberBoxReturn = $NB_NORUN Then Return	; skip AutoIt internal message handler
	Return $GUI_RUNDEFMSG	; run AutoIt internal message handler
EndFunc