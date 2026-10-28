package com.veiller.app.feature.onboarding.consent

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.selection.toggleable
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.designsystem.component.StepProgress
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.ConsentItem
import com.veiller.app.core.model.HealthDataType
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.core.validation.ConsentRules
import com.veiller.app.data.repository.ConsentRepository
import com.veiller.app.navigation.ConsentRoute
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

enum class ConsentMode {
    /** A3 dentro del onboarding del adulto mayor. */
    ONBOARDING,

    /** "Mis permisos" desde el home del adulto mayor. */
    EDIT,

    /** "Consentimientos" desde el círculo, visto por un cuidador. */
    READ_ONLY,
}

data class ConsentUiState(
    val mode: ConsentMode,
    val loading: Boolean = true,
    val items: List<ConsentItem> = emptyList(),
    val disclaimerAccepted: Boolean = false,
    val saving: Boolean = false,
    val done: Boolean = false,
) {
    val canSubmit: Boolean
        get() = !saving && when (mode) {
            ConsentMode.ONBOARDING -> ConsentRules.canAccept(items, disclaimerAccepted)
            ConsentMode.EDIT -> true
            ConsentMode.READ_ONLY -> false
        }
}

class ConsentViewModel(handle: SavedStateHandle, private val repository: ConsentRepository) : ViewModel() {
    private val route = handle.toRoute<ConsentRoute>()
    private val _state = MutableStateFlow(ConsentUiState(mode = ConsentMode.valueOf(route.mode)))
    val state: StateFlow<ConsentUiState> = _state

    init {
        viewModelScope.launch {
            val items = repository.getConsents(route.circleId)
            _state.update { it.copy(loading = false, items = items) }
        }
    }

    fun onToggle(type: HealthDataType, granted: Boolean) {
        if (type.required || _state.value.mode == ConsentMode.READ_ONLY) return
        _state.update { s -> s.copy(items = s.items.map { if (it.type == type) it.copy(granted = granted) else it }) }
    }

    fun onDisclaimerChange(accepted: Boolean) = _state.update { it.copy(disclaimerAccepted = accepted) }

    fun submit() {
        if (!_state.value.canSubmit) return
        _state.update { it.copy(saving = true) }
        viewModelScope.launch {
            repository.saveConsents(_state.value.items)
            _state.update { it.copy(saving = false, done = true) }
        }
    }
}

/** A3 · Tus datos, tu decisión (y sus variantes de edición y solo lectura). */
@Composable
fun ConsentScreen(onBack: (() -> Unit)?, onDone: () -> Unit) {
    val viewModel = veillerViewModel { c, handle -> ConsentViewModel(handle, c.consentRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.done) {
        if (state.done) onDone()
    }
    ConsentContent(state, onBack, viewModel::onToggle, viewModel::onDisclaimerChange, viewModel::submit)
}

@Composable
fun ConsentContent(
    state: ConsentUiState,
    onBack: (() -> Unit)?,
    onToggle: (HealthDataType, Boolean) -> Unit,
    onDisclaimerChange: (Boolean) -> Unit,
    onSubmit: () -> Unit,
) {
    val title = when (state.mode) {
        ConsentMode.ONBOARDING -> "Tus datos, tu decisión"
        ConsentMode.EDIT -> "Mis permisos"
        ConsentMode.READ_ONLY -> "Consentimientos"
    }
    val intro = when (state.mode) {
        ConsentMode.READ_ONLY ->
            "Estos son los datos que la persona titular comparte con su círculo. Solo ella puede cambiarlos."
        else ->
            "Veiller compartirá estos datos solo con los cuidadores que tú apruebes. Puedes retirar el permiso cuando quieras."
    }
    VeillerScreen(
        title = title,
        onBack = onBack,
        bottomBar = if (state.mode == ConsentMode.READ_ONLY) null else {
            {
                PrimaryButton(
                    text = if (state.mode == ConsentMode.ONBOARDING) "Acepto y continúo" else "Guardar cambios",
                    onClick = onSubmit,
                    enabled = state.canSubmit,
                    loading = state.saving,
                )
            }
        },
    ) {
        if (state.mode == ConsentMode.ONBOARDING) {
            StepProgress(current = 1, total = 2, label = "Consentimiento")
            Spacer(Modifier.height(16.dp))
        }
        Text(intro, style = MaterialTheme.typography.bodyMedium, color = VeillerColors.TextSecondary)
        Spacer(Modifier.height(16.dp))
        if (state.loading) {
            CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally))
            return@VeillerScreen
        }
        VeillerCard(contentPadding = PaddingValues(horizontal = 16.dp, vertical = 4.dp)) {
            state.items.forEachIndexed { index, item ->
                if (index > 0) HorizontalDivider(color = VeillerColors.Border)
                ConsentRow(item, editable = state.mode != ConsentMode.READ_ONLY, onToggle = onToggle)
            }
        }
        if (state.mode == ConsentMode.ONBOARDING) {
            Spacer(Modifier.height(16.dp))
            Row(
                Modifier
                    .fillMaxWidth()
                    .toggleable(state.disclaimerAccepted, role = Role.Checkbox, onValueChange = onDisclaimerChange),
                verticalAlignment = Alignment.Top,
            ) {
                Checkbox(
                    checked = state.disclaimerAccepted,
                    onCheckedChange = null,
                    colors = CheckboxDefaults.colors(checkedColor = VeillerColors.Primary),
                )
                Spacer(Modifier.width(10.dp))
                Text(
                    "Entiendo que Veiller no reemplaza atención médica ni servicios de emergencia.",
                    style = MaterialTheme.typography.bodySmall,
                )
            }
        }
    }
}

@Composable
private fun ConsentRow(item: ConsentItem, editable: Boolean, onToggle: (HealthDataType, Boolean) -> Unit) {
    val canChange = editable && !item.type.required
    Row(Modifier.fillMaxWidth().padding(vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(item.type.label, style = MaterialTheme.typography.titleSmall)
            Text(
                if (item.type.required) "Necesario · no se puede desactivar" else "Opcional",
                style = MaterialTheme.typography.labelSmall,
                color = VeillerColors.TextSecondary,
            )
        }
        Switch(
            checked = item.granted,
            onCheckedChange = if (canChange) { checked -> onToggle(item.type, checked) } else null,
            enabled = canChange || item.type.required,
            colors = SwitchDefaults.colors(
                checkedTrackColor = VeillerColors.Primary,
                disabledCheckedTrackColor = VeillerColors.TextPrimary.copy(alpha = 0.75f),
                disabledCheckedThumbColor = VeillerColors.Surface,
                uncheckedTrackColor = VeillerColors.Track,
                uncheckedThumbColor = VeillerColors.Surface,
                uncheckedBorderColor = VeillerColors.Track,
                disabledUncheckedTrackColor = VeillerColors.Track.copy(alpha = 0.6f),
                disabledUncheckedThumbColor = VeillerColors.Surface,
                disabledUncheckedBorderColor = VeillerColors.Track.copy(alpha = 0.6f),
            ),
        )
    }
}

@Preview(showBackground = true)
@Composable
private fun ConsentPreview() {
    VeillerTheme {
        ConsentContent(
            state = ConsentUiState(
                mode = ConsentMode.ONBOARDING,
                loading = false,
                items = HealthDataType.entries.map { ConsentItem(it, it != HealthDataType.ECG) },
            ),
            onBack = {}, onToggle = { _, _ -> }, onDisclaimerChange = {}, onSubmit = {},
        )
    }
}
