package com.veiller.app.core.designsystem.component

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.veiller.app.core.designsystem.theme.VeillerColors

private val ButtonHeight = 52.dp

@Composable
fun PrimaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    loading: Boolean = false,
) {
    Button(
        onClick = onClick,
        enabled = enabled && !loading,
        modifier = modifier.fillMaxWidth().height(ButtonHeight),
        shape = MaterialTheme.shapes.medium,
        colors = ButtonDefaults.buttonColors(
            containerColor = VeillerColors.Primary,
            contentColor = Color.White,
            disabledContainerColor = VeillerColors.Track,
            disabledContentColor = VeillerColors.TextSecondary,
        ),
    ) {
        if (loading) {
            CircularProgressIndicator(Modifier.size(20.dp), color = Color.White, strokeWidth = 2.dp)
        } else {
            Text(text, style = MaterialTheme.typography.labelLarge)
        }
    }
}

@Composable
fun SecondaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    OutlinedButton(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier.fillMaxWidth().height(ButtonHeight),
        shape = MaterialTheme.shapes.medium,
        border = BorderStroke(1.5.dp, if (enabled) VeillerColors.Primary else VeillerColors.Track),
        colors = ButtonDefaults.outlinedButtonColors(contentColor = VeillerColors.Primary),
    ) {
        Text(text, style = MaterialTheme.typography.labelLarge)
    }
}

@Composable
fun LinkButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier) {
    TextButton(onClick = onClick, modifier = modifier) {
        Text(text, style = MaterialTheme.typography.titleSmall, color = VeillerColors.OnPrimaryContainer)
    }
}
