package com.veiller.app.core.designsystem.component

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.veiller.app.core.designsystem.theme.VeillerColors

@Composable
fun VeillerCard(
    modifier: Modifier = Modifier,
    onClick: (() -> Unit)? = null,
    containerColor: Color = VeillerColors.Surface,
    borderColor: Color = VeillerColors.Border,
    contentPadding: PaddingValues = PaddingValues(16.dp),
    content: @Composable ColumnScope.() -> Unit,
) {
    val shape = MaterialTheme.shapes.large
    val border = BorderStroke(1.dp, borderColor)
    val body: @Composable () -> Unit = { Column(Modifier.padding(contentPadding), content = content) }
    if (onClick != null) {
        Surface(onClick = onClick, modifier = modifier.fillMaxWidth(), shape = shape, color = containerColor, border = border, content = body)
    } else {
        Surface(modifier = modifier.fillMaxWidth(), shape = shape, color = containerColor, border = border, content = body)
    }
}

enum class ChipTone(val container: Color, val content: Color) {
    Neutral(VeillerColors.NeutralChip, VeillerColors.TextSecondary),
    Teal(VeillerColors.PrimaryContainer, VeillerColors.OnPrimaryContainer),
    Success(VeillerColors.SuccessContainer, VeillerColors.OnSuccessContainer),
    Warning(VeillerColors.WarningContainer, VeillerColors.OnWarningContainer),
    Info(VeillerColors.InfoContainer, VeillerColors.OnInfoContainer),
    Danger(VeillerColors.DangerContainer, VeillerColors.Danger),
}

@Composable
fun StatusChip(text: String, modifier: Modifier = Modifier, tone: ChipTone = ChipTone.Teal) {
    Text(
        text = text,
        modifier = modifier
            .clip(RoundedCornerShape(50))
            .background(tone.container)
            .padding(horizontal = 8.dp, vertical = 3.dp),
        style = MaterialTheme.typography.labelSmall,
        color = tone.content,
    )
}

@Composable
fun InitialsAvatar(initials: String, modifier: Modifier = Modifier, size: Dp = 40.dp) {
    Box(
        modifier = modifier
            .size(size)
            .clip(CircleShape)
            .background(VeillerColors.PrimaryContainer),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            initials,
            color = VeillerColors.OnPrimaryContainer,
            fontSize = (size.value * 0.32f).sp,
            style = MaterialTheme.typography.titleSmall,
        )
    }
}

enum class BannerTone(val container: Color, val border: Color, val title: Color) {
    Teal(VeillerColors.PrimaryContainer, VeillerColors.OnPrimaryContainer.copy(alpha = 0.4f), VeillerColors.OnPrimaryContainer),
    Warning(VeillerColors.WarningContainer, VeillerColors.WarningBorder, VeillerColors.OnWarningContainer),
    Success(VeillerColors.SuccessContainer, VeillerColors.SuccessBorder, VeillerColors.OnSuccessContainer),
}

@Composable
fun InfoBanner(
    title: String,
    modifier: Modifier = Modifier,
    body: String? = null,
    tone: BannerTone = BannerTone.Teal,
    content: (@Composable ColumnScope.() -> Unit)? = null,
) {
    VeillerCard(modifier = modifier, containerColor = tone.container, borderColor = tone.border) {
        Text(title, style = MaterialTheme.typography.titleSmall, color = tone.title)
        if (body != null) {
            Spacer(Modifier.height(4.dp))
            Text(body, style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextPrimary)
        }
        content?.invoke(this)
    }
}

@Composable
fun SectionHeader(title: String, modifier: Modifier = Modifier, trailing: (@Composable () -> Unit)? = null) {
    Row(
        modifier = modifier.fillMaxWidth().padding(top = 8.dp, bottom = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(title, style = MaterialTheme.typography.titleMedium)
        trailing?.invoke()
    }
}

/** Indicador "Paso X de N · Etiqueta" con barra segmentada. */
@Composable
fun StepProgress(current: Int, total: Int, label: String, modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth()) {
        Text(
            "Paso $current de $total · $label",
            style = MaterialTheme.typography.labelSmall,
            color = VeillerColors.OnPrimaryContainer,
        )
        Spacer(Modifier.height(6.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            repeat(total) { index ->
                Box(
                    Modifier
                        .weight(1f)
                        .height(3.dp)
                        .clip(RoundedCornerShape(50))
                        .background(if (index < current) VeillerColors.OnPrimaryContainer else VeillerColors.Track),
                )
            }
        }
    }
}
