package com.veiller.app.feature.caregiver.history

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.veiller.app.core.designsystem.component.BarChart
import com.veiller.app.core.designsystem.component.ChartEntry
import com.veiller.app.core.designsystem.component.InfoBanner
import com.veiller.app.core.designsystem.component.LinkButton
import com.veiller.app.core.designsystem.component.StatColumn
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.DataPoint
import com.veiller.app.core.model.HistoryRange
import com.veiller.app.core.model.PatientHistory
import com.veiller.app.core.model.PatientSummary
import com.veiller.app.core.model.PlanTier
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.PatientRepository
import com.veiller.app.navigation.HistoryRoute
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

data class HistoryUiState(
    val patient: PatientSummary? = null,
    val range: HistoryRange = HistoryRange.WEEK,
    val history: PatientHistory? = null,
    val loading: Boolean = true,
    val message: String? = null,
) {
    val isFreePlan get() = patient?.plan == PlanTier.FREE
}

class HistoryViewModel(handle: SavedStateHandle, private val patients: PatientRepository) : ViewModel() {
    private val circleId = handle.toRoute<HistoryRoute>().circleId
    private val _state = MutableStateFlow(HistoryUiState())
    val state: StateFlow<HistoryUiState> = _state
    private var loadJob: Job? = null

    init {
        viewModelScope.launch {
            _state.update { it.copy(patient = patients.getPatient(circleId)) }
            load(HistoryRange.WEEK)
        }
    }

    fun onRangeSelected(range: HistoryRange) {
        if (range == HistoryRange.MONTH && _state.value.isFreePlan) {
            _state.update { it.copy(message = "El histórico de 30 días está disponible con Premium") }
            return
        }
        if (range != _state.value.range) load(range)
    }

    fun messageShown() = _state.update { it.copy(message = null) }

    private fun load(range: HistoryRange) {
        loadJob?.cancel()
        _state.update { it.copy(range = range, loading = true) }
        loadJob = viewModelScope.launch {
            val history = patients.getHistory(circleId, range)
            _state.update { it.copy(history = history, loading = false) }
        }
    }
}

/** B5 · Histórico (sin resaltado de anomalías ni exportación PDF). */
@Composable
fun HistoryScreen(onBack: () -> Unit, onOpenPremium: () -> Unit) {
    val viewModel = veillerViewModel { c, handle -> HistoryViewModel(handle, c.patientRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(state.message) {
        state.message?.let { snackbar.showSnackbar(it); viewModel.messageShown() }
    }
    HistoryContent(state, onBack, viewModel::onRangeSelected, onOpenPremium, snackbar)
}

@Composable
fun HistoryContent(
    state: HistoryUiState,
    onBack: () -> Unit,
    onRangeSelected: (HistoryRange) -> Unit,
    onOpenPremium: () -> Unit,
    snackbarHostState: SnackbarHostState? = null,
) {
    VeillerScreen(
        title = "Histórico · ${state.patient?.fullName?.substringBefore(' ').orEmpty()}",
        onBack = onBack,
        snackbarHostState = snackbarHostState,
    ) {
        RangeSelector(state.range, lockMonth = state.isFreePlan, onSelect = onRangeSelected)
        Spacer(Modifier.height(14.dp))
        val history = state.history
        if (state.loading || history == null) {
            CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally).padding(24.dp))
        } else {
            HeartRateCard(history.restingHeartRate)
            Spacer(Modifier.height(12.dp))
            SleepCard(history.range, history.sleepHours)
        }
        if (state.isFreePlan) {
            Spacer(Modifier.height(12.dp))
            InfoBanner(
                title = "Plan gratuito: últimos 7 días",
                body = "Con Premium el círculo ve el histórico completo y exporta reportes para el médico.",
            ) {
                LinkButton("Ver Premium ›", onClick = onOpenPremium)
            }
        }
    }
}

@Composable
private fun RangeSelector(selected: HistoryRange, lockMonth: Boolean, onSelect: (HistoryRange) -> Unit) {
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(VeillerColors.PrimaryContainer.copy(alpha = 0.6f))
            .padding(4.dp),
    ) {
        HistoryRange.entries.forEach { range ->
            val isSelected = range == selected
            Box(
                Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(10.dp))
                    .background(if (isSelected) VeillerColors.Surface else VeillerColors.PrimaryContainer.copy(alpha = 0f))
                    .selectable(isSelected, role = Role.Tab, onClick = { onSelect(range) })
                    .padding(vertical = 10.dp),
                contentAlignment = Alignment.Center,
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        range.label,
                        style = if (isSelected) MaterialTheme.typography.titleSmall else MaterialTheme.typography.bodyMedium,
                        color = if (isSelected) VeillerColors.TextPrimary else VeillerColors.TextSecondary,
                    )
                    if (range == HistoryRange.MONTH && lockMonth) {
                        Spacer(Modifier.width(4.dp))
                        Icon(Icons.Filled.Lock, contentDescription = "Premium", modifier = Modifier.size(12.dp), tint = VeillerColors.OnWarningContainer)
                    }
                }
            }
        }
    }
}

@Composable
private fun ChartCardHeader(title: String, trailing: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(title, style = MaterialTheme.typography.titleMedium)
        Text(trailing, style = MaterialTheme.typography.labelSmall, color = VeillerColors.TextSecondary)
    }
}

@Composable
private fun NoData() {
    Spacer(Modifier.height(12.dp))
    Text("Sin datos del reloj para este periodo.", style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
}

@Composable
private fun HeartRateCard(points: List<DataPoint>) {
    VeillerCard {
        ChartCardHeader("FC en reposo", "lpm")
        val values = points.mapNotNull { it.value }
        if (values.isEmpty()) {
            NoData()
            return@VeillerCard
        }
        Spacer(Modifier.height(14.dp))
        BarChart(points.map { ChartEntry(it.label, it.value) }, description = "Frecuencia cardíaca en reposo")
        Spacer(Modifier.height(12.dp))
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            StatColumn("Prom.", values.average().roundToInt().toString())
            StatColumn("Mín.", values.min().roundToInt().toString(), alignment = Alignment.CenterHorizontally)
            StatColumn("Máx.", values.max().roundToInt().toString(), alignment = Alignment.End)
        }
    }
}

@Composable
private fun SleepCard(range: HistoryRange, points: List<DataPoint>) {
    val values = points.mapNotNull { it.value }
    VeillerCard {
        val average = if (values.isEmpty() || range == HistoryRange.DAY) "" else "prom. ${Format.hours(values.average().toFloat())}"
        ChartCardHeader("Horas de sueño", average)
        when {
            values.isEmpty() -> NoData()
            range == HistoryRange.DAY -> {
                Spacer(Modifier.height(8.dp))
                StatColumn(points.first().label, Format.hours(values.first()))
            }
            else -> {
                Spacer(Modifier.height(14.dp))
                BarChart(points.map { ChartEntry(it.label, it.value) }, description = "Horas de sueño por día")
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun HistoryPreview() {
    val labels = listOf("J", "V", "S", "D", "L", "M", "X")
    VeillerTheme {
        HistoryContent(
            state = HistoryUiState(
                patient = PatientSummary("1", "Rosa Martínez", 78, PlanTier.FREE, 2, 128, 310, 96, null, 42, 8),
                history = PatientHistory(
                    HistoryRange.WEEK,
                    labels.zip(listOf(76f, 77f, 75f, 78f, 76f, 79f, 80f)) { l, v -> DataPoint(l, v) },
                    labels.zip(listOf(7.2f, 6.8f, 7.5f, 6.9f, 6.8f, 7.1f, 5.2f)) { l, v -> DataPoint(l, v) },
                ),
                loading = false,
            ),
            onBack = {}, onRangeSelected = {}, onOpenPremium = {},
        )
    }
}
