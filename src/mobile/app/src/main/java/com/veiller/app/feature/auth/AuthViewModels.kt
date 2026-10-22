package com.veiller.app.feature.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.veiller.app.core.model.Role
import com.veiller.app.core.validation.FormValidators
import com.veiller.app.data.repository.AuthRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class RegisterUiState(
    val role: Role,
    val fullName: String = "",
    val email: String = "",
    val password: String = "",
    val showErrors: Boolean = false,
    val loading: Boolean = false,
    val error: String? = null,
    val registered: Boolean = false,
) {
    val nameError get() = FormValidators.nameError(fullName).takeIf { showErrors }
    val emailError get() = FormValidators.emailError(email).takeIf { showErrors }
    val passwordError get() = FormValidators.passwordError(password).takeIf { showErrors }
    val isValid get() =
        FormValidators.nameError(fullName) == null &&
            FormValidators.emailError(email) == null &&
            FormValidators.passwordError(password) == null
}

class RegisterViewModel(role: Role, private val auth: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(RegisterUiState(role))
    val state: StateFlow<RegisterUiState> = _state

    fun onNameChange(value: String) = _state.update { it.copy(fullName = value, error = null) }
    fun onEmailChange(value: String) = _state.update { it.copy(email = value, error = null) }
    fun onPasswordChange(value: String) = _state.update { it.copy(password = value, error = null) }

    fun submit() {
        _state.update { it.copy(showErrors = true) }
        val current = _state.value
        if (!current.isValid || current.loading) return
        _state.update { it.copy(loading = true) }
        viewModelScope.launch {
            auth.register(current.fullName, current.email, current.password, current.role)
                .onSuccess { _state.update { it.copy(loading = false, registered = true) } }
                .onFailure { e -> _state.update { it.copy(loading = false, error = e.message) } }
        }
    }
}

data class LoginUiState(
    val email: String = "",
    val password: String = "",
    val loading: Boolean = false,
    val error: String? = null,
    val loggedInAs: Role? = null,
) {
    val canSubmit get() = email.isNotBlank() && password.isNotBlank() && !loading
}

class LoginViewModel(private val auth: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(LoginUiState())
    val state: StateFlow<LoginUiState> = _state

    fun onEmailChange(value: String) = _state.update { it.copy(email = value, error = null) }
    fun onPasswordChange(value: String) = _state.update { it.copy(password = value, error = null) }

    fun submit() {
        val current = _state.value
        if (!current.canSubmit) return
        _state.update { it.copy(loading = true) }
        viewModelScope.launch {
            auth.login(current.email, current.password)
                .onSuccess { user -> _state.update { it.copy(loading = false, loggedInAs = user.role) } }
                .onFailure { e -> _state.update { it.copy(loading = false, error = e.message) } }
        }
    }
}
