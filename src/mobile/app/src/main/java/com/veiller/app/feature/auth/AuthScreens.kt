package com.veiller.app.feature.auth

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.veiller.app.core.designsystem.component.InfoBanner
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.designsystem.component.StatusChip
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.component.VeillerTextField
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.Role
import com.veiller.app.core.ui.veillerViewModel

/** A2 · Crear cuenta. */
@Composable
fun RegisterScreen(role: Role, onBack: () -> Unit, onRegistered: (Role) -> Unit) {
    val viewModel = veillerViewModel { c, _ -> RegisterViewModel(role, c.authRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.registered) {
        if (state.registered) onRegistered(state.role)
    }
    RegisterContent(
        state = state,
        onBack = onBack,
        onNameChange = viewModel::onNameChange,
        onEmailChange = viewModel::onEmailChange,
        onPasswordChange = viewModel::onPasswordChange,
        onSubmit = viewModel::submit,
    )
}

@Composable
fun RegisterContent(
    state: RegisterUiState,
    onBack: () -> Unit,
    onNameChange: (String) -> Unit,
    onEmailChange: (String) -> Unit,
    onPasswordChange: (String) -> Unit,
    onSubmit: () -> Unit,
) {
    VeillerScreen(title = "Crear cuenta", onBack = onBack) {
        StatusChip("Rol: ${state.role.label}")
        Spacer(Modifier.height(16.dp))
        VeillerTextField(
            label = "Nombre completo",
            value = state.fullName,
            onValueChange = onNameChange,
            placeholder = "Ej. Laura Gómez",
            error = state.nameError,
            keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Words, imeAction = ImeAction.Next),
        )
        Spacer(Modifier.height(14.dp))
        EmailField(state.email, onEmailChange, state.emailError)
        Spacer(Modifier.height(14.dp))
        PasswordField(state.password, onPasswordChange, state.passwordError, placeholder = "Mínimo 8 caracteres", onDone = onSubmit)
        Spacer(Modifier.height(20.dp))
        if (state.error != null) {
            Text(state.error, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
            Spacer(Modifier.height(8.dp))
        }
        PrimaryButton("Crear cuenta", onClick = onSubmit, loading = state.loading)
        // Va en el contenido y no fijo abajo para que no tape el botón cuando el teclado está abierto.
        Spacer(Modifier.height(16.dp))
        Text(
            "Al continuar aceptas los Términos y la Política de tratamiento de datos de salud.",
            modifier = Modifier.fillMaxWidth(),
            style = MaterialTheme.typography.bodySmall,
            color = VeillerColors.TextSecondary,
            textAlign = TextAlign.Center,
        )
    }
}

/** Inicio de sesión (no está en los mockups; sigue el estilo de A2). */
@Composable
fun LoginScreen(onBack: () -> Unit, onLoggedIn: (Role) -> Unit) {
    val viewModel = veillerViewModel { c, _ -> LoginViewModel(c.authRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.loggedInAs) {
        state.loggedInAs?.let(onLoggedIn)
    }
    LoginContent(state, onBack, viewModel::onEmailChange, viewModel::onPasswordChange, viewModel::submit)
}

@Composable
fun LoginContent(
    state: LoginUiState,
    onBack: () -> Unit,
    onEmailChange: (String) -> Unit,
    onPasswordChange: (String) -> Unit,
    onSubmit: () -> Unit,
) {
    VeillerScreen(title = "Iniciar sesión", onBack = onBack) {
        EmailField(state.email, onEmailChange, error = null)
        Spacer(Modifier.height(14.dp))
        PasswordField(state.password, onPasswordChange, error = state.error, placeholder = "Tu contraseña", onDone = onSubmit)
        Spacer(Modifier.height(20.dp))
        PrimaryButton("Iniciar sesión", onClick = onSubmit, enabled = state.canSubmit, loading = state.loading)
        Spacer(Modifier.height(20.dp))
        InfoBanner(
            title = "Modo demostración",
            body = "Aún no hay backend. Usa laura@correo.com (cuidador) o rosa@correo.com (adulto mayor) " +
                "con cualquier contraseña de 8 caracteres, o el correo con el que te registraste.",
        )
    }
}

@Composable
private fun EmailField(value: String, onValueChange: (String) -> Unit, error: String?) {
    VeillerTextField(
        label = "Correo electrónico",
        value = value,
        onValueChange = onValueChange,
        placeholder = "laura@correo.com",
        error = error,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next),
    )
}

@Composable
private fun PasswordField(
    value: String,
    onValueChange: (String) -> Unit,
    error: String?,
    placeholder: String,
    onDone: () -> Unit,
) {
    val keyboard = LocalSoftwareKeyboardController.current
    VeillerTextField(
        label = "Contraseña",
        value = value,
        onValueChange = onValueChange,
        placeholder = placeholder,
        error = error,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
        keyboardActions = KeyboardActions(onDone = { keyboard?.hide(); onDone() }),
        visualTransformation = PasswordVisualTransformation(),
    )
}

@Preview(showBackground = true)
@Composable
private fun RegisterPreview() {
    VeillerTheme { RegisterContent(RegisterUiState(Role.CAREGIVER), {}, {}, {}, {}, {}) }
}
