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
import androidx.compose.material.icons.filled.Warning
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
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
import com.veiller.app.core.designsystem.component.MetricCard
import com.veiller.app.core.designsystem.component.SectionHeader
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.icon.VeillerIcons
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.CircleMember
import com.veiller.app.core.model.ElderHome
import com.veiller.app.core.model.HealthDataType
import com.veiller.app.core.model.MemberRole
import com.veiller.app.core.ui.Format
import com.veiller.app.core.ui.dial
import com.veiller.app.core.ui.veillerViewModel
import com.veiller.app.data.health.HealthAvailability
import com.veiller.app.data.health.HealthMetric
import com.veiller.app.data.health.HealthSnapshot
import com.veiller.app.data.health.HeartRateReading
import com.veiller.app.data.health.SleepReading
import com.veiller.app.data.health.StepsReading
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.ElderRepository
import com.veiller.app.data.repository.HealthRepository
import com.veiller.app.feature.health.HealthAvailabilityBanner
import com.veiller.app.feature.health.MissingHealthAccessBanner
import com.veiller.app.feature.health.rememberHealthPermissionRequest
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.Duration
import java.time.Instant
import java.time.ZoneId

data class ElderHomeUiState(
    val firstName: String = "",
    val home: ElderHome? = null,
    val healthAvailability: HealthAvailability = HealthAvailability.AVAILABLE,
    /** Última lectura de Health Connect; null si no está disponible o aún no se leyó. */
    val health: HealthSnapshot? = null,
    val refreshingHealth: Boolean = false,
    /** Permisos de Health Connect por pedir: la UI abre el diálogo y avisa con onPermissionRequestShown. */
    val permissionRequest: Set<String>? = null,
    val loggedOut: Boolean = false,
) {
    /** Tipos sin permiso según la última lectura (p. ej. un elder que inició sesión en un teléfono nuevo). */
    val missingHealth: Set<HealthDataType>
        get() = health?.missing.orEmpty()
}

class ElderHomeViewModel(
    private val auth: AuthRepository,
    private val elder: ElderRepository,
    private val health: HealthRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(ElderHomeUiState())
    val state: StateFlow<ElderHomeUiState> = _state

    fun refresh() {
        viewModelScope.launch {
            val name = auth.session.first()?.user?.firstName.orEmpty()
            _state.update { it.copy(firstName = name, home = elder.getHome()) }
        }
        refreshHealth()
    }

    /** Lee Health Connect una vez. Sin polling: se llama al volver a la pantalla y con "Actualizar". */
    fun refreshHealth() {
        if (_state.value.refreshingHealth) return
        val availability = health.availability()
        _state.update { it.copy(healthAvailability = availability, refreshingHealth = true) }
        viewModelScope.launch {
            val snapshot = if (availability == HealthAvailability.AVAILABLE) health.readToday() else null
            _state.update { it.copy(health = snapshot, refreshingHealth = false) }
        }
    }

    fun requestHealthAccess(types: Set<HealthDataType>) =
        _state.update { it.copy(permissionRequest = health.permissionsFor(types)) }

    fun onPermissionRequestShown() = _state.update { it.copy(permissionRequest = null) }

    fun logout() = viewModelScope.launch {
        auth.logout()
        _state.update { it.copy(loggedOut = true) }
    }
}

/** C4 · Home del adulto mayor (sin el botón "Avisar a mis cuidadores"). */
@Composable
fun ElderHomeScreen(onOpenPermissions: () -> Unit, onLoggedOut: () -> Unit) {
    val viewModel = veillerViewModel { c, _ -> ElderHomeViewModel(c.authRepository, c.elderRepository, c.healthRepository) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val requestHealthPermissions = rememberHealthPermissionRequest(onResult = viewModel::refreshHealth)
    LifecycleResumeEffect(Unit) {
        viewModel.refresh()
        onPauseOrDispose { }
    }
    LaunchedEffect(state.permissionRequest) {
        state.permissionRequest?.let {
            viewModel.onPermissionRequestShown()
            requestHealthPermissions(it)
        }
    }
    LaunchedEffect(state.loggedOut) { if (state.loggedOut) onLoggedOut() }
    ElderHomeContent(
        state = state,
        onCall = { context.dial(it) },
        onOpenPermissions = onOpenPermissions,
        onRefreshHealth = viewModel::refreshHealth,
        onGrantHealth = viewModel::requestHealthAccess,
        onLogout = { viewModel.logout() },
    )
}

@Composable
fun ElderHomeContent(
    state: ElderHomeUiState,
    onCall: (String) -> Unit,
    onOpenPermissions: () -> Unit,
    onRefreshHealth: () -> Unit,
    onGrantHealth: (Set<HealthDataType>) -> Unit,
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
        if (state.healthAvailability != HealthAvailability.AVAILABLE) {
            HealthAvailabilityBanner(state.healthAvailability)
            Spacer(Modifier.height(12.dp))
        }
        val missing = state.missingHealth
        if (missing.isNotEmpty()) {
            MissingHealthAccessBanner(missing, onGrant = { onGrantHealth(missing) })
            Spacer(Modifier.height(12.dp))
        }
        val health = state.health
        if (health != null) {
            // Sin ningún dato y con permisos pendientes, el aviso de permisos ya explica el estado.
            if (health.lastWatchDataAt != null || missing.isEmpty()) {
                StatusCard(health)
                Spacer(Modifier.height(12.dp))
            }
            WatchCard(health)
            Spacer(Modifier.height(12.dp))
            TodayHealthSection(health, onRefreshHealth, onGrant = { onGrantHealth(setOf(it)) })
            Spacer(Modifier.height(12.dp))
        }
        CaregiversCard(home.caregivers, onCall)
    }
}

/** Estado general según la antigüedad real de los datos del reloj, nunca "todo bien" por defecto. */
@Composable
private fun StatusCard(health: HealthSnapshot) {
    when {
        health.lastWatchDataAt == null -> StatusCardContent(
            title = "Aún no hay datos de tu reloj",
            body = "Revisa que Mi Fitness esté conectado a Health Connect.",
            icon = Icons.Filled.Warning,
            container = VeillerColors.WarningContainer,
            border = VeillerColors.WarningBorder,
            content = VeillerColors.OnWarningContainer,
        )
        health.isWatchDataStale -> StatusCardContent(
            title = "Sin datos recientes del reloj",
            body = "Ponte el reloj y abre Mi Fitness para que se sincronice.",
            icon = Icons.Filled.Warning,
            container = VeillerColors.WarningContainer,
            border = VeillerColors.WarningBorder,
            content = VeillerColors.OnWarningContainer,
        )
        else -> StatusCardContent(
            title = "Todo en orden",
            body = "Tu reloj está enviando datos.",
            icon = Icons.Filled.Check,
            container = VeillerColors.SuccessContainer,
            border = VeillerColors.SuccessBorder,
            content = VeillerColors.OnSuccessContainer,
        )
    }
}

@Composable
private fun StatusCardContent(title: String, body: String, icon: ImageVector, container: Color, border: Color, content: Color) {
    VeillerCard(containerColor = container, borderColor = border) {
        Column(Modifier.fillMaxWidth().padding(vertical = 8.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Box(
                Modifier.size(52.dp).clip(CircleShape).background(VeillerColors.Surface),
                contentAlignment = Alignment.Center,
            ) {
                Icon(icon, contentDescription = null, tint = content, modifier = Modifier.size(30.dp))
            }
            Spacer(Modifier.height(12.dp))
            Text(title, style = MaterialTheme.typography.headlineSmall, color = content, textAlign = TextAlign.Center)
            Spacer(Modifier.height(6.dp))
            Text(body, style = MaterialTheme.typography.bodyLarge, textAlign = TextAlign.Center)
        }
    }
}

/** Health Connect no expone batería ni conexión del reloj: solo cuándo llegó el último dato. */
@Composable
private fun WatchCard(health: HealthSnapshot) {
    VeillerCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(40.dp).clip(CircleShape).background(VeillerColors.PrimaryContainer), contentAlignment = Alignment.Center) {
                Icon(VeillerIcons.Watch, contentDescription = null, modifier = Modifier.size(20.dp), tint = VeillerColors.TextPrimary)
            }
            Spacer(Modifier.width(14.dp))
            Column {
                Text("Tu reloj", style = MaterialTheme.typography.titleSmall)
                val last = health.lastWatchDataAt
                Text(
                    if (last != null) "Últimos datos del reloj: ${Format.agoSince(last, health.readAt)}" else "Sin datos del reloj todavía",
                    style = MaterialTheme.typography.bodySmall,
                    color = VeillerColors.TextSecondary,
                )
            }
        }
    }
}

@Composable
private fun TodayHealthSection(health: HealthSnapshot, onRefresh: () -> Unit, onGrant: (HealthDataType) -> Unit) {
    val zone = ZoneId.systemDefault()
    val now = health.readAt
    SectionHeader("Tus datos de hoy", trailing = { LinkButton("Actualizar", onClick = onRefresh) })
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        HealthMetricCard(
            label = "Ritmo cardíaco",
            metric = health.heartRate,
            noDataText = "Aún no hay datos de ritmo cardíaco. Revisa que el monitoreo continuo esté activo en el reloj.",
            onGrant = { onGrant(HealthDataType.HEART_RATE) },
        ) { reading: HeartRateReading ->
            val measured = Format.measuredAt(reading.measuredAt, now, zone)
            MetricValue(
                value = reading.beatsPerMinute.toString(),
                unit = "lpm",
                // Una FC vieja (p. ej. solo de un entrenamiento) no significa que el reloj esté apagado.
                caption = if (reading.isOld(now)) "Última medición registrada: $measured" else "Medido $measured",
            )
        }
        HealthMetricCard(
            label = "Pasos de hoy",
            metric = health.steps,
            noDataText = "Aún no hay pasos registrados hoy.",
            onGrant = { onGrant(HealthDataType.STEPS) },
        ) { reading: StepsReading ->
            MetricValue(
                value = Format.count(reading.count),
                unit = "pasos",
                caption = reading.lastRecordAt?.let { "Último registro ${Format.measuredAt(it, now, zone)}" } ?: "Hoy",
            )
        }
        HealthMetricCard(
            label = "Sueño",
            metric = health.sleep,
            noDataText = "Aún no hay datos de sueño. Usa el reloj al dormir.",
            onGrant = { onGrant(HealthDataType.SLEEP) },
        ) { reading: SleepReading ->
            val end = Format.measuredAt(reading.end, now, zone)
            MetricValue(
                value = Format.sleep(reading.duration.toMinutes().toInt()),
                unit = null,
                caption = if (reading.isOld(now)) "Última sesión registrada: terminó $end" else "Te despertaste $end",
            )
        }
    }
}

/**
 * Las tarjetas no deciden si el reloj está sin datos: eso lo hace solo el encabezado,
 * con [HealthSnapshot.isWatchDataStale], para que nunca se contradigan.
 */
private data class MetricValue(val value: String, val unit: String?, val caption: String)

@Composable
private fun <T> HealthMetricCard(
    label: String,
    metric: HealthMetric<T>,
    noDataText: String,
    onGrant: () -> Unit,
    present: (T) -> MetricValue,
) {
    when (metric) {
        is HealthMetric.Value -> {
            val shown = present(metric.reading)
            MetricCard(label = label, value = shown.value, unit = shown.unit) {
                Spacer(Modifier.height(4.dp))
                Text(shown.caption, style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
            }
        }
        HealthMetric.NotPermitted -> MetricCard(label = label, value = EMPTY_VALUE) {
            Text("Veiller no tiene permiso para leer este dato.", style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
            LinkButton("Conceder acceso", onClick = onGrant)
        }
        HealthMetric.NoData -> MetricCard(label = label, value = EMPTY_VALUE) {
            Text(noDataText, style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
        }
        HealthMetric.ReadFailed -> MetricCard(label = label, value = EMPTY_VALUE) {
            Text("No se pudo leer el dato. Toca Actualizar para reintentar.", style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
        }
    }
}

private const val EMPTY_VALUE = "—"

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
    val now = Instant.parse("2026-11-01T22:30:00Z")
    VeillerTheme {
        ElderHomeContent(
            state = ElderHomeUiState(
                firstName = "Rosa",
                home = ElderHome(
                    caregivers = listOf(
                        CircleMember("1", "Laura Gómez", MemberRole.ADMIN, relation = "hija", phone = "300"),
                        CircleMember("2", "Juan Rodríguez", MemberRole.CAREGIVER, relation = "sobrino", phone = "310"),
                    ),
                ),
                health = HealthSnapshot(
                    heartRate = HealthMetric.Value(HeartRateReading(145, now.minus(Duration.ofHours(8)), PREVIEW_SOURCE)),
                    steps = HealthMetric.Value(StepsReading(8894, now.minus(Duration.ofHours(9)), setOf(PREVIEW_SOURCE))),
                    sleep = HealthMetric.NoData,
                    lastWatchDataAt = now.minus(Duration.ofMinutes(20)),
                    missing = emptySet(),
                    readAt = now,
                ),
            ),
            onCall = {}, onOpenPermissions = {}, onRefreshHealth = {}, onGrantHealth = {}, onLogout = {},
        )
    }
}

private const val PREVIEW_SOURCE = "com.example.reloj"
