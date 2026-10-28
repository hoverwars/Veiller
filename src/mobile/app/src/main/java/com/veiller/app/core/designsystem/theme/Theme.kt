package com.veiller.app.core.designsystem.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

private val LightColors = lightColorScheme(
    primary = VeillerColors.Primary,
    onPrimary = Color.White,
    primaryContainer = VeillerColors.PrimaryContainer,
    onPrimaryContainer = VeillerColors.OnPrimaryContainer,
    secondary = VeillerColors.OnPrimaryContainer,
    onSecondary = Color.White,
    background = VeillerColors.Background,
    onBackground = VeillerColors.TextPrimary,
    surface = VeillerColors.Surface,
    onSurface = VeillerColors.TextPrimary,
    surfaceVariant = VeillerColors.Background,
    onSurfaceVariant = VeillerColors.TextSecondary,
    // Evita el tinte lila por defecto de Material 3 en diálogos, menús y switches.
    surfaceContainerLowest = VeillerColors.Surface,
    surfaceContainerLow = VeillerColors.Surface,
    surfaceContainer = VeillerColors.Surface,
    surfaceContainerHigh = VeillerColors.Surface,
    surfaceContainerHighest = VeillerColors.NeutralChip,
    outline = VeillerColors.Border,
    outlineVariant = VeillerColors.Border,
    error = VeillerColors.Danger,
)

private val VeillerShapes = Shapes(
    small = RoundedCornerShape(8.dp),
    medium = RoundedCornerShape(12.dp),
    large = RoundedCornerShape(16.dp),
)

// Los mockups solo definen tema claro.
@Composable
fun VeillerTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = LightColors,
        typography = VeillerTypography,
        shapes = VeillerShapes,
        content = content,
    )
}
