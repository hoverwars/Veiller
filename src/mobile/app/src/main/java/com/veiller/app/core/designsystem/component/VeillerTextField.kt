package com.veiller.app.core.designsystem.component

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldColors
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import com.veiller.app.core.designsystem.theme.VeillerColors

/** Campo con la etiqueta encima, como en los mockups. */
@Composable
fun VeillerTextField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    modifier: Modifier = Modifier,
    placeholder: String? = null,
    error: String? = null,
    enabled: Boolean = true,
    keyboardOptions: KeyboardOptions = KeyboardOptions.Default,
    keyboardActions: KeyboardActions = KeyboardActions.Default,
    visualTransformation: VisualTransformation = VisualTransformation.None,
    textStyle: TextStyle = MaterialTheme.typography.bodyLarge,
) {
    LabeledField(label, modifier) {
        OutlinedTextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth(),
            enabled = enabled,
            singleLine = true,
            textStyle = textStyle,
            placeholder = placeholder?.let { { Text(it, style = textStyle, color = VeillerColors.TextSecondary) } },
            isError = error != null,
            supportingText = error?.let { { Text(it) } },
            keyboardOptions = keyboardOptions,
            keyboardActions = keyboardActions,
            visualTransformation = visualTransformation,
            shape = MaterialTheme.shapes.medium,
            colors = veillerFieldColors(),
        )
    }
}

/** Variante con [TextFieldValue] para campos que reformatean el texto y deben controlar el cursor. */
@Composable
fun VeillerTextField(
    label: String,
    value: TextFieldValue,
    onValueChange: (TextFieldValue) -> Unit,
    modifier: Modifier = Modifier,
    placeholder: String? = null,
    error: String? = null,
    keyboardOptions: KeyboardOptions = KeyboardOptions.Default,
    keyboardActions: KeyboardActions = KeyboardActions.Default,
    textStyle: TextStyle = MaterialTheme.typography.bodyLarge,
) {
    LabeledField(label, modifier) {
        OutlinedTextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            textStyle = textStyle,
            placeholder = placeholder?.let { { Text(it, style = textStyle, color = VeillerColors.TextSecondary) } },
            isError = error != null,
            supportingText = error?.let { { Text(it) } },
            keyboardOptions = keyboardOptions,
            keyboardActions = keyboardActions,
            shape = MaterialTheme.shapes.medium,
            colors = veillerFieldColors(),
        )
    }
}

@Composable
private fun LabeledField(label: String, modifier: Modifier, field: @Composable () -> Unit) {
    Column(modifier) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = VeillerColors.TextPrimary)
        Spacer(Modifier.height(6.dp))
        field()
    }
}

@Composable
private fun veillerFieldColors(): TextFieldColors = OutlinedTextFieldDefaults.colors(
    focusedContainerColor = VeillerColors.Surface,
    unfocusedContainerColor = VeillerColors.Surface,
    disabledContainerColor = VeillerColors.Surface,
    focusedBorderColor = VeillerColors.OnPrimaryContainer,
    unfocusedBorderColor = VeillerColors.Border,
)
