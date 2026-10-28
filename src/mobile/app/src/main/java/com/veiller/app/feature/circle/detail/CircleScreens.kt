package com.veiller.app.feature.circle.detail

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
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
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.designsystem.component.StatusChip
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.Circle
import com.veiller.app.core.model.CircleMember
import com.veiller.app.core.model.JoinRequest
import com.veiller.app.core.model.MemberRole
import com.veiller.app.core.model.PatientSummary
import com.veiller.app.core.model.PlanTier
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.CircleRepository
import com.veiller.app.data.repository.PatientRepository
import com.veiller.app.feature.circle.JoinRequestCard
import com.veiller.app.feature.circle.displayName
import com.veiller.app.feature.circle.planName
import com.veiller.app.navigation.CircleDetailRoute
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

// ---------- Pestaña Círculo: lista de círculos ----------

class CirclesViewModel(patients: PatientRepository) : ViewModel() {
    val patients: StateFlow<List<PatientSummary>?> = patients.observePatients()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), null)
}

/** Lista mínima para elegir círculo desde la barra inferior (no está en los mockups). */
@Composable
fun CirclesScreen(onOpenCircle: (String) -> Unit) {
    val viewModel = veillerViewModel { c, _ -> CirclesViewModel(c.patientRepository) }
    val patients by viewModel.patients.collectAsStateWithLifecycle()
    VeillerScreen(title = "Mis círculos") {
        Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
            patients.orEmpty().forEach { patient ->
                VeillerCard(onClick = { onOpenCircle(patient.circleId) }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        InitialsAvatar(patient.initials)
                        Spacer(Modifier.width(12.dp))
                        Column(Modifier.weight(1f)) {
                            Text("Círculo de ${patient.fullName.substringBefore(' ')}", style = MaterialTheme.typography.titleSmall)
                            Text(
                                "${patient.plan.planName()} · ${Format.caregivers(patient.caregiverCount)}",
                                style = MaterialTheme.typography.bodySmall,
                                color = VeillerColors.TextSecondary,
                            )
                        }
                        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = null, tint = VeillerColors.TextSecondary)
                    }
                }
            }
        }
    }
}

// ---------- C1 · Círculo ----------

data class CircleUiState(
    val circle: Circle? = null,
    val currentUserId: String? = null,
    val message: String? = null,
) {
    val isAdmin: Boolean get() = circle?.admin?.userId == currentUserId
}

class CircleViewModel(
    handle: SavedStateHandle,
    private val circles: CircleRepository,
    auth: AuthRepository,
) : ViewModel() {
    private val circleId = handle.toRoute<CircleDetailRoute>().circleId
    private val message = MutableStateFlow<String?>(null)

    val state: StateFlow<CircleUiState> =
        combine(circles.observeCircle(circleId), auth.session, message) { circle, session, msg ->
            CircleUiState(circle, session?.user?.id, msg)
        }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), CircleUiState())

    fun approve(requestId: String) = viewModelScope.launch {
        circles.approveRequest(circleId, requestId).onFailure { message.value = it.message }
    }

    fun reject(requestId: String) = viewModelScope.launch { circles.rejectRequest(circleId, requestId) }

    fun remove(userId: String) = viewModelScope.launch { circles.removeMember(circleId, userId) }

    fun messageShown() {
        message.value = null
    }
}

@Composable
fun CircleScreen(
    onBack: () -> Unit,
    onUpgrade: () -> Unit,
    onOpenAlertRules: () -> Unit,
    onOpenConsents: (String) -> Unit,
) {
    val viewModel = veillerViewModel { c, handle -> CircleViewModel(handle, c.circleRepository, c.authRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(state.message) {
        state.message?.let { snackbar.showSnackbar(it); viewModel.messageShown() }
    }
    CircleContent(
        state = state,
        onBack = onBack,
        onUpgrade = onUpgrade,
        onApprove = { viewModel.approve(it) },
        onReject = { viewModel.reject(it) },
        onRemove = { viewModel.remove(it) },
        onOpenAlertRules = onOpenAlertRules,
        onOpenConsents = { state.circle?.let { onOpenConsents(it.id) } },
        snackbarHostState = snackbar,
    )
}

@Composable
fun CircleContent(
    state: CircleUiState,
    onBack: () -> Unit,
    onUpgrade: () -> Unit,
    onApprove: (String) -> Unit,
    onReject: (String) -> Unit,
    onRemove: (String) -> Unit,
    onOpenAlertRules: () -> Unit,
    onOpenConsents: () -> Unit,
    snackbarHostState: SnackbarHostState? = null,
) {
    val circle = state.circle
    val ownerFirstName = circle?.owner?.fullName?.substringBefore(' ').orEmpty()
    VeillerScreen(title = "Círculo de $ownerFirstName", onBack = onBack, snackbarHostState = snackbarHostState) {
        if (circle == null) {
            CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally))
            return@VeillerScreen
        }
        PlanCard(circle, onUpgrade)
        Spacer(Modifier.height(16.dp))
        Text("Miembros", style = MaterialTheme.typography.labelMedium, color = VeillerColors.TextSecondary)
        Spacer(Modifier.height(8.dp))
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            circle.members.forEach { member ->
                MemberCard(
                    member = member,
                    isCurrentUser = member.userId == state.currentUserId,
                    canRemove = state.isAdmin && member.role != MemberRole.DATA_OWNER && member.userId != state.currentUserId,
                    onRemove = { onRemove(member.userId) },
                )
            }
            circle.pendingRequests.forEach { request: JoinRequest ->
                JoinRequestCard(
                    request = request,
                    canApprove = state.isAdmin,
                    onApprove = { onApprove(request.id) },
                    onReject = { onReject(request.id) },
                    blockedReason = if (circle.isFull) {
                        "Límite del ${circle.plan.planName().lowercase()} alcanzado. Mejora a Premium o retira a un cuidador."
                    } else {
                        null
                    },
                )
            }
            NavRow("Reglas de alerta", "Umbrales de FC, sueño, zonas e inactividad", onOpenAlertRules)
            NavRow("Consentimientos", "Qué datos comparte $ownerFirstName", onOpenConsents)
        }
    }
}

@Composable
private fun PlanCard(circle: Circle, onUpgrade: () -> Unit) {
    VeillerCard {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text(circle.plan.planName(), style = MaterialTheme.typography.titleMedium)
            StatusChip(
                "${circle.caregivers.size} de ${circle.plan.maxCaregivers} cuidadores",
                tone = if (circle.isFull) ChipTone.Warning else ChipTone.Teal,
            )
        }
        if (circle.plan == PlanTier.FREE) {
            Spacer(Modifier.height(12.dp))
            PrimaryButton("Mejorar a Premium", onClick = onUpgrade)
        }
    }
}

@Composable
private fun MemberCard(member: CircleMember, isCurrentUser: Boolean, canRemove: Boolean, onRemove: () -> Unit) {
    var menuOpen by remember { mutableStateOf(false) }
    var confirmRemove by remember { mutableStateOf(false) }
    if (confirmRemove) {
        AlertDialog(
            onDismissRequest = { confirmRemove = false },
            title = { Text("¿Retirar a ${member.fullName}?") },
            text = { Text("Dejará de ver los datos y de recibir las alertas de este círculo.") },
            confirmButton = {
                TextButton(onClick = { confirmRemove = false; onRemove() }) {
                    Text("Retirar", color = VeillerColors.Danger)
                }
            },
            dismissButton = { TextButton(onClick = { confirmRemove = false }) { Text("Cancelar") } },
        )
    }
    VeillerCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            InitialsAvatar(member.initials)
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(member.displayName(isCurrentUser), style = MaterialTheme.typography.titleSmall)
                StatusChip(if (member.isSponsor) "${member.role.label} · Sponsor" else member.role.label)
            }
            if (canRemove) {
                Box {
                    IconButton(onClick = { menuOpen = true }) {
                        Icon(Icons.Filled.MoreVert, contentDescription = "Opciones de ${member.fullName}", tint = VeillerColors.TextSecondary)
                    }
                    DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                        DropdownMenuItem(text = { Text("Retirar del círculo") }, onClick = { menuOpen = false; confirmRemove = true })
                    }
                }
            }
        }
    }
}

@Composable
private fun NavRow(title: String, subtitle: String, onClick: () -> Unit) {
    VeillerCard(onClick = onClick) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.titleSmall)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
            }
            Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = null)
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun CirclePreview() {
    val rosa = CircleMember("u-rosa", "Rosa Martínez", MemberRole.DATA_OWNER)
    VeillerTheme {
        CircleContent(
            state = CircleUiState(
                circle = Circle(
                    id = "c",
                    owner = rosa,
                    ownerAge = 78,
                    plan = PlanTier.FREE,
                    members = listOf(
                        rosa,
                        CircleMember("u-laura", "Laura Gómez", MemberRole.ADMIN, isSponsor = true),
                        CircleMember("u-juan", "Juan Rodríguez", MemberRole.CAREGIVER),
                    ),
                    pendingRequests = listOf(JoinRequest("r", "Pedro Ruiz")),
                ),
                currentUserId = "u-laura",
            ),
            onBack = {}, onUpgrade = {}, onApprove = {}, onReject = {}, onRemove = {}, onOpenAlertRules = {}, onOpenConsents = {},
        )
    }
}
