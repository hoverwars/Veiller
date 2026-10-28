package com.veiller.app.core.designsystem.component

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.veiller.app.core.designsystem.theme.VeillerColors

/** Tarjeta de métrica del detalle de paciente (B2), sin indicadores de estado. */
@Composable
fun MetricCard(label: String, value: String, modifier: Modifier = Modifier, unit: String? = null) {
    VeillerCard(modifier = modifier) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = VeillerColors.TextSecondary)
        Spacer(Modifier.height(6.dp))
        Row(verticalAlignment = Alignment.Bottom) {
            Text(value, style = MaterialTheme.typography.displayMedium, color = VeillerColors.TextPrimary)
            if (unit != null) {
                Spacer(Modifier.width(4.dp))
                Text(
                    unit,
                    style = MaterialTheme.typography.bodySmall,
                    color = VeillerColors.TextSecondary,
                    modifier = Modifier.padding(bottom = 6.dp),
                )
            }
        }
    }
}

data class ChartEntry(val label: String, val value: Float?)

/** Barras simples dibujadas con Canvas. Valores nulos se dejan vacíos. */
@Composable
fun BarChart(
    entries: List<ChartEntry>,
    modifier: Modifier = Modifier,
    barColor: Color = VeillerColors.ChartBar,
    chartHeight: Dp = 72.dp,
    description: String = "",
) {
    val max = entries.mapNotNull { it.value }.maxOrNull()?.takeIf { it > 0f } ?: 1f
    Column(modifier.fillMaxWidth()) {
        Canvas(
            Modifier
                .fillMaxWidth()
                .height(chartHeight)
                .semantics { contentDescription = description },
        ) {
            if (entries.isEmpty()) return@Canvas
            val slot = size.width / entries.size
            val barWidth = slot * 0.72f
            entries.forEachIndexed { index, entry ->
                val value = entry.value ?: return@forEachIndexed
                val barHeight = size.height * (value / (max * 1.05f))
                drawRoundRect(
                    color = barColor,
                    topLeft = Offset(index * slot + (slot - barWidth) / 2f, size.height - barHeight),
                    size = Size(barWidth, barHeight),
                    cornerRadius = CornerRadius(6f, 6f),
                )
            }
        }
        Spacer(Modifier.height(6.dp))
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
            entries.forEach {
                Text(
                    it.label,
                    modifier = Modifier.weight(1f),
                    textAlign = TextAlign.Center,
                    style = MaterialTheme.typography.labelSmall,
                    color = VeillerColors.TextSecondary,
                )
            }
        }
    }
}

/** Par etiqueta/valor apilado, p. ej. "Prom. 78". */
@Composable
fun StatColumn(label: String, value: String, modifier: Modifier = Modifier, alignment: Alignment.Horizontal = Alignment.Start) {
    Column(modifier, horizontalAlignment = alignment) {
        Text(label, style = MaterialTheme.typography.labelSmall, color = VeillerColors.TextSecondary)
        Text(value, style = MaterialTheme.typography.headlineSmall)
    }
}
