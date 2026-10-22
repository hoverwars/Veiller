package com.veiller.app.feature.caregiver.patient

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.veiller.app.core.designsystem.component.ChipTone
import com.veiller.app.core.designsystem.component.MetricCard
import com.veiller.app.core.designsystem.component.SecondaryButton
import com.veiller.app.core.designsystem.component.StatusChip
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.PatientSummary
import com.veiller.app.core.model.PlanTier
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.PatientRepository
import com.veiller.app.navigation.PatientDetailRoute
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch

class PatientDetailViewModel(handle: SavedStateHandle, patients: PatientRepository) : ViewModel() {
    val circleId = handle.toRoute<PatientDetailRoute>().circleId
    private val _patient = MutableStateFlow<PatientSummary?>(null)
    val patient: StateFlow<PatientSummary?> = _patient

    init {
        viewModelScope.launch { _patient.value = patients.getPatient(circleId) }
    }
}

/** B2 · Detalle del paciente (sin estados, análisis del día ni ubicación). */
@Composable
fun PatientDetailScreen(onBack: () -> Unit, onOpenHistory: (String) -> Unit, onOpenCircle: (String) -> Unit) {
    val viewModel = veillerViewModel { c, handle -> PatientDetailViewModel(handle, c.patientRepository) }
    val patient by viewModel.patient.collectAsStateWithLifecycle()
    PatientDetailContent(
        patient = patient,
        onBack = onBack,
        onOpenHistory = { onOpenHistory(viewModel.circleId) },
        onOpenCircle = { onOpenCircle(viewModel.circleId) },
    )
}

@Composable
fun PatientDetailContent(
    patient: PatientSummary?,
    onBack: () -> Unit,
    onOpenHistory: () -> Unit,
    onOpenCircle: () -> Unit,
) {
    var menuOpen by remember { mutableStateOf(false) }
    VeillerScreen(
        title = patient?.fullName.orEmpty(),
        onBack = onBack,
        actions = {
            Box {
                IconButton(onClick = { menuOpen = true }) { Icon(Icons.Filled.MoreVert, contentDescription = "Más opciones") }
                DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                    DropdownMenuItem(text = { Text("Ver círculo") }, onClick = { menuOpen = false; onOpenCircle() })
                }
            }
        },
        bottomBar = if (patient != null) {
            { SecondaryButton("Ver histórico y análisis", onClick = onOpenHistory) }
        } else {
            null
        },
    ) {
        if (patient == null) {
            CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally))
            return@VeillerScreen
        }
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            StatusChip("Sincronizado hace ${Format.ago(patient.lastSyncMinutesAgo)}", tone = ChipTone.Neutral)
            patient.batteryPercent?.let { StatusChip("Batería $it %", tone = ChipTone.Neutral) }
        }
        Spacer(Modifier.height(14.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            MetricCard("Frec. cardíaca", patient.heartRateBpm?.toString() ?: "—", Modifier.weight(1f), unit = patient.heartRateBpm?.let { "lpm" })
            MetricCard("Sueño", Format.sleepClock(patient.sleepMinutes), Modifier.weight(1f), unit = patient.sleepMinutes?.let { "h" })
        }
        Spacer(Modifier.height(12.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            MetricCard("SpO₂", patient.spo2Percent?.toString() ?: "—", Modifier.weight(1f), unit = patient.spo2Percent?.let { "%" })
            MetricCard("Presión", patient.bloodPressure ?: "—", Modifier.weight(1f))
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun PatientDetailPreview() {
    VeillerTheme {
        PatientDetailContent(
            patient = PatientSummary("1", "Rosa Martínez", 78, PlanTier.FREE, 2, 128, 310, 96, null, 42, 8),
            onBack = {}, onOpenHistory = {}, onOpenCircle = {},
        )
    }
}
