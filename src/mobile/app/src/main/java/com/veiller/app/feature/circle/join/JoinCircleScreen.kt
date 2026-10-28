package com.veiller.app.feature.circle.join

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import com.veiller.app.core.designsystem.component.BannerTone
import com.veiller.app.core.designsystem.component.ChipTone
import com.veiller.app.core.designsystem.component.InfoBanner
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.designsystem.component.SecondaryButton
import com.veiller.app.core.designsystem.component.StatusChip
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.component.VeillerTextField
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.CirclePreview
import com.veiller.app.core.model.PlanTier
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.core.validation.CircleCode
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.CircleRepository
import com.veiller.app.feature.circle.planName
import com.veiller.app.navigation.JoinCircleRoute
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class JoinCircleUiState(
    val fromOnboarding: Boolean,
    val code: String = "",
    val lookingUp: Boolean = false,
    val preview: CirclePreview? = null,
    val lookupError: String? = null,
    val sending: Boolean = false,
    val sent: Boolean = false,
    val done: Boolean = false,
)

class JoinCircleViewModel(
    handle: SavedStateHandle,
    private val circles: CircleRepository,
    private val auth: AuthRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(JoinCircleUiState(fromOnboarding = handle.toRoute<JoinCircleRoute>().fromOnboarding))
    val state: StateFlow<JoinCircleUiState> = _state
    private var lookupJob: Job? = null

    fun onCodeChange(input: String) {
        val code = CircleCode.format(input)
        if (code == _state.value.code) return
        lookupJob?.cancel()
        _state.update { it.copy(code = code, preview = null, lookupError = null, lookingUp = false) }
        if (!CircleCode.isValid(code)) return
        _state.update { it.copy(lookingUp = true) }
        lookupJob = viewModelScope.launch {
            circles.findByCode(code)
                .onSuccess { preview -> _state.update { it.copy(lookingUp = false, preview = preview) } }
                .onFailure { e -> _state.update { it.copy(lookingUp = false, lookupError = e.message) } }
        }
    }

    fun sendRequest() {
        val current = _state.value
        if (current.preview == null || current.sending) return
        _state.update { it.copy(sending = true) }
        viewModelScope.launch {
            circles.requestToJoin(current.code)
                .onSuccess { _state.update { it.copy(sending = false, sent = true) } }
                .onFailure { e -> _state.update { it.copy(sending = false, lookupError = e.message) } }
        }
    }

    fun finish() = viewModelScope.launch {
        if (_state.value.fromOnboarding) auth.completeOnboarding()
        _state.update { it.copy(done = true) }
    }
}

/** A6 · Unirse a un círculo. */
@Composable
fun JoinCircleScreen(onBack: (() -> Unit)?, onDone: (fromOnboarding: Boolean) -> Unit) {
    val viewModel = veillerViewModel { c, handle -> JoinCircleViewModel(handle, c.circleRepository, c.authRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    LaunchedEffect(state.done) { if (state.done) onDone(state.fromOnboarding) }

    JoinCircleContent(
        state = state,
        snackbarHostState = snackbar,
        onBack = onBack,
        onCodeChange = viewModel::onCodeChange,
        onSend = viewModel::sendRequest,
        onScan = {
            val options = GmsBarcodeScannerOptions.Builder().setBarcodeFormats(Barcode.FORMAT_QR_CODE).build()
            GmsBarcodeScanning.getClient(context, options).startScan()
                .addOnSuccessListener { barcode ->
                    barcode.rawValue?.let { viewModel.onCodeChange(it.substringAfterLast('/')) }
                }
                .addOnFailureListener {
                    scope.launch { snackbar.showSnackbar("No se pudo abrir el escáner de QR") }
                }
        },
        onFinish = { viewModel.finish() },
    )
}

@Composable
fun JoinCircleContent(
    state: JoinCircleUiState,
    onBack: (() -> Unit)?,
    onCodeChange: (String) -> Unit,
    onSend: () -> Unit,
    onScan: () -> Unit,
    onFinish: () -> Unit,
    snackbarHostState: SnackbarHostState? = null,
) {
    if (state.sent) {
        val owner = state.preview?.ownerName?.substringBefore(' ') ?: "La persona"
        VeillerScreen(
            title = "Unirse a un círculo",
            bottomBar = { PrimaryButton(if (state.fromOnboarding) "Ir al inicio" else "Volver", onClick = onFinish) },
        ) {
            Spacer(Modifier.height(8.dp))
            InfoBanner(
                title = "Solicitud enviada",
                body = "Te avisaremos cuando $owner o quien administra el círculo la apruebe.",
                tone = BannerTone.Success,
            )
        }
        return
    }

    VeillerScreen(
        title = "Unirse a un círculo",
        onBack = onBack,
        snackbarHostState = snackbarHostState,
        bottomBar = {
            PrimaryButton("Enviar solicitud", onClick = onSend, enabled = state.preview != null, loading = state.sending)
            SecondaryButton("Escanear código QR", onClick = onScan)
        },
    ) {
        Text(
            "Ingresa el código que aparece en el teléfono de la persona que vas a cuidar.",
            style = MaterialTheme.typography.bodyMedium,
            color = VeillerColors.TextSecondary,
        )
        Spacer(Modifier.height(16.dp))
        // El formato cambia el largo del texto ("7" -> "VLR-7"): el cursor siempre va al final.
        val codeFieldValue = remember(state.code) { TextFieldValue(state.code, TextRange(state.code.length)) }
        VeillerTextField(
            label = "Código del círculo",
            value = codeFieldValue,
            onValueChange = { onCodeChange(it.text) },
            placeholder = "VLR-XXXX",
            error = state.lookupError,
            textStyle = MaterialTheme.typography.titleLarge,
            keyboardOptions = KeyboardOptions(
                capitalization = KeyboardCapitalization.Characters,
                keyboardType = KeyboardType.Ascii,
                autoCorrectEnabled = false,
                imeAction = ImeAction.Done,
            ),
        )
        Spacer(Modifier.height(12.dp))
        if (state.lookingUp) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                Spacer(Modifier.width(10.dp))
                Text("Buscando círculo…", style = MaterialTheme.typography.bodySmall)
            }
        }
        state.preview?.let { preview ->
            CirclePreviewCard(preview)
            Spacer(Modifier.height(12.dp))
            InfoBanner(
                title = "Requiere aprobación",
                body = "${preview.ownerName.substringBefore(' ')} o quien administra el círculo deben aprobar tu " +
                    "solicitud antes de que veas sus datos.",
            )
        }
    }
}

@Composable
private fun CirclePreviewCard(preview: CirclePreview) {
    VeillerCard {
        Row(verticalAlignment = Alignment.Top) {
            InitialsAvatar(preview.ownerInitials)
            Spacer(Modifier.width(12.dp))
            Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text("${preview.ownerName}, ${preview.ownerAge} años", style = MaterialTheme.typography.titleSmall)
                preview.adminName?.let {
                    Text("Administra: $it", style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
                }
                Spacer(Modifier.height(6.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    StatusChip(preview.plan.planName(), tone = ChipTone.Neutral)
                    StatusChip("${preview.caregiverCount} de ${preview.plan.maxCaregivers} cuidadores")
                }
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun JoinCirclePreview() {
    VeillerTheme {
        JoinCircleContent(
            state = JoinCircleUiState(
                fromOnboarding = true,
                code = "VLR-7K2Q",
                preview = CirclePreview("VLR-7K2Q", "Rosa Martínez", 78, "Laura Gómez", PlanTier.FREE, 1),
            ),
            onBack = {}, onCodeChange = {}, onSend = {}, onScan = {}, onFinish = {},
        )
    }
}
