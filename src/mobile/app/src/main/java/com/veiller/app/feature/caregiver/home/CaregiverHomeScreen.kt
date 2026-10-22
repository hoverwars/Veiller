package com.veiller.app.feature.caregiver.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.SecondaryButton
import com.veiller.app.core.designsystem.component.SectionHeader
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.icon.VeillerIcons
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.PatientSummary
import com.veiller.app.core.model.PlanTier
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.PatientRepository
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn

data class CaregiverHomeUiState(
    val firstName: String = "",
    val patients: List<PatientSummary> = emptyList(),
)

class CaregiverHomeViewModel(auth: AuthRepository, patients: PatientRepository) : ViewModel() {
    val state: StateFlow<CaregiverHomeUiState> =
        combine(auth.session, patients.observePatients()) { session, list ->
            CaregiverHomeUiState(session?.user?.firstName.orEmpty(), list)
        }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), CaregiverHomeUiState())
}

/** B1 · Inicio del cuidador (sin banner de alertas ni estados de paciente). */
@Composable
fun CaregiverHomeScreen(onOpenPatient: (String) -> Unit, onJoinCircle: () -> Unit, onOpenAlerts: () -> Unit) {
    val viewModel = veillerViewModel { c, _ -> CaregiverHomeViewModel(c.authRepository, c.patientRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    CaregiverHomeContent(state, onOpenPatient, onJoinCircle, onOpenAlerts)
}

@Composable
fun CaregiverHomeContent(
    state: CaregiverHomeUiState,
    onOpenPatient: (String) -> Unit,
    onJoinCircle: () -> Unit,
    onOpenAlerts: () -> Unit,
) {
    VeillerScreen(
        title = "Hola, ${state.firstName}",
        actions = {
            IconButton(onClick = onOpenAlerts) {
                Icon(Icons.Outlined.Notifications, contentDescription = "Alertas")
            }
        },
    ) {
        SectionHeader("Mis pacientes") {
            Text(
                if (state.patients.size == 1) "1 círculo" else "${state.patients.size} círculos",
                style = MaterialTheme.typography.bodySmall,
                color = VeillerColors.TextSecondary,
            )
        }
        Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
            state.patients.forEach { PatientCard(it, onClick = { onOpenPatient(it.circleId) }) }
        }
        Spacer(Modifier.height(20.dp))
        SecondaryButton("+ Unirse a otro círculo", onClick = onJoinCircle)
    }
}

@Composable
private fun PatientCard(patient: PatientSummary, onClick: () -> Unit) {
    VeillerCard(onClick = onClick) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            InitialsAvatar(patient.initials)
            Spacer(Modifier.width(12.dp))
            Column {
                Text("${patient.fullName}, ${patient.age}", style = MaterialTheme.typography.titleSmall)
                Text(
                    "${patient.plan.label} · ${Format.caregivers(patient.caregiverCount)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = VeillerColors.TextSecondary,
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        Row(Modifier, horizontalArrangement = Arrangement.SpaceBetween) {
            InlineMetric(Icons.Filled.Favorite, VeillerColors.Danger, patient.heartRateBpm?.let { "$it lpm" } ?: "—", Modifier.weight(1f))
            InlineMetric(VeillerIcons.Moon, VeillerColors.TextSecondary, Format.sleep(patient.sleepMinutes), Modifier.weight(1f))
            InlineMetric(Icons.Filled.Refresh, VeillerColors.TextSecondary, Format.ago(patient.lastSyncMinutesAgo))
        }
    }
}

@Composable
private fun InlineMetric(icon: ImageVector, tint: Color, text: String, modifier: Modifier = Modifier) {
    Row(modifier, verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(14.dp))
        Spacer(Modifier.width(4.dp))
        Text(text, style = MaterialTheme.typography.bodySmall)
    }
}

@Preview(showBackground = true)
@Composable
private fun CaregiverHomePreview() {
    VeillerTheme {
        CaregiverHomeContent(
            state = CaregiverHomeUiState(
                firstName = "Laura",
                patients = listOf(
                    PatientSummary("1", "Rosa Martínez", 78, PlanTier.FREE, 2, 128, 310, 96, null, 42, 8),
                    PatientSummary("2", "Elena Gómez", 69, PlanTier.FREE, 1, null, null, null, null, null, 180),
                ),
            ),
            onOpenPatient = {}, onJoinCircle = {}, onOpenAlerts = {},
        )
    }
}
