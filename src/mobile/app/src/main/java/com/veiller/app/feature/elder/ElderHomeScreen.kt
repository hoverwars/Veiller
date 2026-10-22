package com.veiller.app.feature.elder

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.LinkButton
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.icon.VeillerIcons
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.CircleMember
import com.veiller.app.core.model.ElderHome
import com.veiller.app.core.model.MemberRole
import com.veiller.app.core.model.WatchStatus
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.dial
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.ElderRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class ElderHomeUiState(
    val firstName: String = "",
    val home: ElderHome? = null,
    val loggedOut: Boolean = false,
)

class ElderHomeViewModel(private val auth: AuthRepository, private val elder: ElderRepository) : ViewModel() {
    private val _state = MutableStateFlow(ElderHomeUiState())
    val state: StateFlow<ElderHomeUiState> = _state

    fun refresh() = viewModelScope.launch {
        val name = auth.session.first()?.user?.firstName.orEmpty()
        _state.update { it.copy(firstName = name, home = elder.getHome()) }
    }

    fun logout() = viewModelScope.launch {
        auth.logout()
        _state.update { it.copy(loggedOut = true) }
    }
}

/** C4 · Home del adulto mayor (sin el botón "Avisar a mis cuidadores"). */
@Composable
fun ElderHomeScreen(onOpenPermissions: () -> Unit, onLoggedOut: () -> Unit) {
    val viewModel = veillerViewModel { c, _ -> ElderHomeViewModel(c.authRepository, c.elderRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    LifecycleResumeEffect(Unit) {
        viewModel.refresh()
        onPauseOrDispose { }
    }
    LaunchedEffect(state.loggedOut) { if (state.loggedOut) onLoggedOut() }
    ElderHomeContent(
        state = state,
        onCall = { context.dial(it) },
        onOpenPermissions = onOpenPermissions,
        onLogout = { viewModel.logout() },
    )
}

@Composable
fun ElderHomeContent(
    state: ElderHomeUiState,
    onCall: (String) -> Unit,
    onOpenPermissions: () -> Unit,
    onLogout: () -> Unit,
) {
    var menuOpen by remember { mutableStateOf(false) }
    VeillerScreen(
        title = "Hola, ${state.firstName}",
        actions = {
            Box {
                IconButton(onClick = { menuOpen = true }) { Icon(Icons.Filled.MoreVert, contentDescription = "Más opciones") }
                DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                    DropdownMenuItem(text = { Text("Cerrar sesión") }, onClick = { menuOpen = false; onLogout() })
                }
            }
        },
        bottomBar = { LinkButton("Mis permisos", onClick = onOpenPermissions) },
    ) {
        val home = state.home
        if (home == null) {
            CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally))
            return@VeillerScreen
        }
        StatusCard(home.lastSendMinutesAgo)
        Spacer(Modifier.height(12.dp))
        WatchCard(home.watch)
        Spacer(Modifier.height(12.dp))
        CaregiversCard(home.caregivers, onCall)
    }
}

@Composable
private fun StatusCard(lastSendMinutesAgo: Int) {
    VeillerCard(containerColor = VeillerColors.SuccessContainer, borderColor = VeillerColors.SuccessBorder) {
        Column(Modifier.fillMaxWidth().padding(vertical = 8.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Box(
                Modifier.size(52.dp).clip(CircleShape).background(VeillerColors.Surface),
                contentAlignment = Alignment.Center,
            ) {
                Icon(Icons.Filled.Check, contentDescription = null, tint = VeillerColors.OnSuccessContainer, modifier = Modifier.size(30.dp))
            }
            Spacer(Modifier.height(12.dp))
            Text("Todo en orden", style = MaterialTheme.typography.headlineSmall, color = VeillerColors.OnSuccessContainer)
            Spacer(Modifier.height(6.dp))
            Text(
                "Tus cuidadores reciben tus datos.\nÚltimo envío: hace ${Format.ago(lastSendMinutesAgo)}.",
                style = MaterialTheme.typography.bodyLarge,
                textAlign = TextAlign.Center,
            )
        }
    }
}

@Composable
private fun WatchCard(watch: WatchStatus) {
    VeillerCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(40.dp).clip(CircleShape).background(VeillerColors.PrimaryContainer), contentAlignment = Alignment.Center) {
                Icon(VeillerIcons.Watch, contentDescription = null, modifier = Modifier.size(20.dp), tint = VeillerColors.TextPrimary)
            }
            Spacer(Modifier.width(14.dp))
            Column {
                Text("Tu reloj", style = MaterialTheme.typography.titleSmall)
                val status = if (watch.connected) "Conectado" else "Desconectado"
                Text(
                    watch.batteryPercent?.let { "$status · batería $it %" } ?: status,
                    style = MaterialTheme.typography.bodySmall,
                    color = VeillerColors.TextSecondary,
                )
            }
        }
    }
}

@Composable
private fun CaregiversCard(caregivers: List<CircleMember>, onCall: (String) -> Unit) {
    VeillerCard {
        Text("Te cuidan", style = MaterialTheme.typography.titleMedium)
        Spacer(Modifier.height(8.dp))
        if (caregivers.isEmpty()) {
            Text("Aún no tienes cuidadores en tu círculo.", style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
        }
        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
            caregivers.forEach { member ->
                Row(Modifier.heightIn(min = 48.dp), verticalAlignment = Alignment.CenterVertically) {
                    InitialsAvatar(member.initials, size = 36.dp)
                    Spacer(Modifier.width(12.dp))
                    val name = member.fullName.substringBefore(' ')
                    Text(member.relation?.let { "$name ($it)" } ?: name, style = MaterialTheme.typography.bodyLarge, modifier = Modifier.weight(1f))
                    if (member.phone != null) {
                        IconButton(onClick = { onCall(member.phone) }) {
                            Icon(Icons.Filled.Phone, contentDescription = "Llamar a $name")
                        }
                    }
                }
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun ElderHomePreview() {
    VeillerTheme {
        ElderHomeContent(
            state = ElderHomeUiState(
                firstName = "Rosa",
                home = ElderHome(
                    lastSendMinutesAgo = 8,
                    watch = WatchStatus(true, 42),
                    caregivers = listOf(
                        CircleMember("1", "Laura Gómez", MemberRole.ADMIN, relation = "hija", phone = "300"),
                        CircleMember("2", "Juan Rodríguez", MemberRole.CAREGIVER, relation = "sobrino", phone = "310"),
                    ),
                ),
            ),
            onCall = {}, onOpenPermissions = {}, onLogout = {},
        )
    }
}
