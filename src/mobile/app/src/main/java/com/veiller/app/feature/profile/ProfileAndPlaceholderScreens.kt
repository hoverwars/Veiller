package com.veiller.app.feature.profile

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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.SecondaryButton
import com.veiller.app.core.designsystem.component.StatusChip
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.model.Session
import com.veiller.app.core.model.initialsOf
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.AuthRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class ProfileViewModel(private val auth: AuthRepository) : ViewModel() {
    val session: StateFlow<Session?> = auth.session.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), null)
    private val _loggedOut = MutableStateFlow(false)
    val loggedOut: StateFlow<Boolean> = _loggedOut

    fun logout() = viewModelScope.launch {
        auth.logout()
        _loggedOut.value = true
    }
}

/** Perfil del cuidador (placeholder con cierre de sesión). */
@Composable
fun ProfileScreen(onLoggedOut: () -> Unit) {
    val viewModel = veillerViewModel { c, _ -> ProfileViewModel(c.authRepository) }
    val session by viewModel.session.collectAsStateWithLifecycle()
    val loggedOut by viewModel.loggedOut.collectAsStateWithLifecycle()
    LaunchedEffect(loggedOut) { if (loggedOut) onLoggedOut() }

    VeillerScreen(title = "Perfil") {
        session?.user?.let { user ->
            VeillerCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    InitialsAvatar(initialsOf(user.fullName), size = 48.dp)
                    Spacer(Modifier.width(14.dp))
                    Column {
                        Text(user.fullName, style = MaterialTheme.typography.titleMedium)
                        Text(user.email, style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
                        Spacer(Modifier.height(6.dp))
                        StatusChip(user.role.label)
                    }
                }
            }
        }
        Spacer(Modifier.height(20.dp))
        SecondaryButton("Cerrar sesión", onClick = { viewModel.logout() })
    }
}

/** Pantalla genérica para secciones que todavía no se implementan. */
@Composable
fun ComingSoonScreen(title: String, onBack: (() -> Unit)?) {
    VeillerScreen(title = title, onBack = onBack) {
        Text(
            "Esta sección estará disponible pronto.",
            modifier = Modifier.fillMaxWidth().padding(top = 48.dp),
            style = MaterialTheme.typography.bodyLarge,
            color = VeillerColors.TextSecondary,
            textAlign = TextAlign.Center,
        )
    }
}
