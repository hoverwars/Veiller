package com.veiller.app.feature.onboarding.invite

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.veiller.app.core.designsystem.component.ChipTone
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.designsystem.component.QrCode
import com.veiller.app.core.designsystem.component.SecondaryButton
import com.veiller.app.core.designsystem.component.SectionHeader
import com.veiller.app.core.designsystem.component.StatusChip
import com.veiller.app.core.designsystem.component.StepProgress
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.CircleCodeTextStyle
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.Circle
import com.veiller.app.core.model.InviteCode
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.shareText
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.CircleRepository
import com.veiller.app.feature.circle.JoinRequestCard
import com.veiller.app.feature.circle.MemberRow
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.Duration
import java.time.Instant

data class InviteUiState(
    val ownerFirstName: String = "",
    val invite: InviteCode? = null,
    val generating: Boolean = true,
    val circle: Circle? = null,
    val message: String? = null,
    val finished: Boolean = false,
)

class InviteViewModel(
    private val circles: CircleRepository,
    private val auth: AuthRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(InviteUiState())
    val state: StateFlow<InviteUiState> = _state

    init {
        viewModelScope.launch {
            val name = auth.session.first()?.user?.firstName.orEmpty()
            _state.update { it.copy(ownerFirstName = name) }
        }
        viewModelScope.launch {
            circles.observeCircle(circles.ownCircleId).collect { circle -> _state.update { it.copy(circle = circle) } }
        }
        newCode()
    }

    fun newCode() {
        _state.update { it.copy(generating = true) }
        viewModelScope.launch {
            val invite = circles.generateInviteCode(circles.ownCircleId)
            _state.update { it.copy(invite = invite, generating = false) }
        }
    }

    fun approve(requestId: String) = viewModelScope.launch {
        circles.approveRequest(circles.ownCircleId, requestId)
            .onFailure { e -> _state.update { it.copy(message = e.message) } }
    }

    fun reject(requestId: String) = viewModelScope.launch { circles.rejectRequest(circles.ownCircleId, requestId) }

    fun messageShown() = _state.update { it.copy(message = null) }

    fun finish() = viewModelScope.launch {
        auth.completeOnboarding()
        _state.update { it.copy(finished = true) }
    }
}

/** A5 · Invita a tus cuidadores (paso 2 de 2 del onboarding del adulto mayor). */
@Composable
fun InviteScreen(onBack: (() -> Unit)?, onFinished: () -> Unit) {
    val viewModel = veillerViewModel { c, _ -> InviteViewModel(c.circleRepository, c.authRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }

    LaunchedEffect(state.finished) { if (state.finished) onFinished() }
    LaunchedEffect(state.message) {
        state.message?.let { snackbar.showSnackbar(it); viewModel.messageShown() }
    }

    InviteContent(
        state = state,
        snackbarHostState = snackbar,
        onBack = onBack,
        onShare = { code ->
            context.shareText("Únete al círculo de cuidado de ${state.ownerFirstName} en Veiller con el código $code")
        },
        onNewCode = viewModel::newCode,
        onApprove = { viewModel.approve(it) },
        onReject = { viewModel.reject(it) },
        onFinish = { viewModel.finish() },
    )
}

@Composable
fun InviteContent(
    state: InviteUiState,
    onBack: (() -> Unit)?,
    onShare: (String) -> Unit,
    onNewCode: () -> Unit,
    onApprove: (String) -> Unit,
    onReject: (String) -> Unit,
    onFinish: () -> Unit,
    snackbarHostState: SnackbarHostState? = null,
) {
    VeillerScreen(
        title = "Invita a tus cuidadores",
        onBack = onBack,
        snackbarHostState = snackbarHostState,
        bottomBar = { PrimaryButton("Terminar", onClick = onFinish) },
    ) {
        StepProgress(current = 2, total = 2, label = "Círculo de cuidado")
        Spacer(Modifier.height(16.dp))
        InviteCodeCard(state.ownerFirstName, state.invite, state.generating)
        Spacer(Modifier.height(12.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            PrimaryButton(
                "Compartir",
                onClick = { state.invite?.let { onShare(it.code) } },
                enabled = state.invite != null,
                modifier = Modifier.weight(1f),
            )
            SecondaryButton("Nuevo código", onClick = onNewCode, enabled = !state.generating, modifier = Modifier.weight(1f))
        }
        Spacer(Modifier.height(12.dp))

        val circle = state.circle ?: return@VeillerScreen
        SectionHeader("Cuidadores vinculados") {
            StatusChip("${circle.caregivers.size} de ${circle.plan.maxCaregivers} · ${circle.plan.label}", tone = ChipTone.Neutral)
        }
        if (circle.caregivers.isEmpty()) {
            Text(
                "Aún no hay cuidadores. Comparte el código para que se unan.",
                style = MaterialTheme.typography.bodySmall,
                color = VeillerColors.TextSecondary,
            )
        }
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            circle.caregivers.forEach { MemberRow(it) }
            circle.pendingRequests.forEach { request ->
                JoinRequestCard(
                    request = request,
                    canApprove = true,
                    onApprove = { onApprove(request.id) },
                    onReject = { onReject(request.id) },
                    blockedReason = if (circle.isFull) "Límite del plan alcanzado. Retira a un cuidador o mejora a Premium." else null,
                )
            }
        }
    }
}

@Composable
private fun InviteCodeCard(ownerFirstName: String, invite: InviteCode?, generating: Boolean) {
    var now by remember { mutableStateOf(Instant.now()) }
    LaunchedEffect(invite) {
        while (true) {
            now = Instant.now()
            delay(30_000)
        }
    }
    VeillerCard {
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            Text("Código del círculo de $ownerFirstName", style = MaterialTheme.typography.bodyMedium)
            Spacer(Modifier.height(8.dp))
            if (invite == null || generating) {
                Box(Modifier.height(196.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator(Modifier.size(32.dp)) }
                return@Column
            }
            Text(invite.code, style = CircleCodeTextStyle)
            Spacer(Modifier.height(6.dp))
            val remaining = Duration.between(now, invite.expiresAt)
            StatusChip(Format.remaining(remaining), tone = if (remaining.isNegative) ChipTone.Danger else ChipTone.Warning)
            Spacer(Modifier.height(14.dp))
            QrCode(content = "veiller://join/${invite.code}")
            Spacer(Modifier.height(4.dp))
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun InvitePreview() {
    VeillerTheme {
        InviteContent(
            state = InviteUiState(
                ownerFirstName = "Rosa",
                invite = InviteCode("VLR-7K2Q", Instant.now().plus(Duration.ofHours(23))),
                generating = false,
            ),
            onBack = {}, onShare = {}, onNewCode = {}, onApprove = {}, onReject = {}, onFinish = {},
        )
    }
}
