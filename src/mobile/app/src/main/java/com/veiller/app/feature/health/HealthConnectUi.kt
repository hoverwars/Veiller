package com.veiller.app.feature.health

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.net.toUri
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.PermissionController
import com.veiller.app.core.designsystem.component.BannerTone
import com.veiller.app.core.designsystem.component.InfoBanner
import com.veiller.app.core.designsystem.component.LinkButton
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.model.HealthDataType
import com.veiller.app.data.health.HealthAvailability
import com.veiller.app.data.health.HealthConfig

/**
 * Devuelve una función que abre el diálogo de permisos de Health Connect.
 * [onResult] no recibe lo concedido: quien llama debe volver a consultar los
 * permisos, porque el usuario puede conceder solo una parte.
 */
@Composable
fun rememberHealthPermissionRequest(onResult: () -> Unit): (Set<String>) -> Unit {
    val launcher = rememberLauncherForActivityResult(PermissionController.createRequestPermissionResultContract()) {
        onResult()
    }
    return { permissions -> launcher.launch(permissions) }
}

fun Context.openHealthConnectSettings() {
    try {
        startActivity(Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS))
    } catch (_: ActivityNotFoundException) {
        // Sin Health Connect instalado no hay ajustes que abrir; el aviso de disponibilidad ya lo explica.
    }
}

fun Context.openHealthConnectInStore() {
    val intent = Intent(Intent.ACTION_VIEW, HealthConfig.PROVIDER_STORE_URI.toUri())
        .setPackage(HealthConfig.PLAY_STORE_PACKAGE)
    try {
        startActivity(intent)
    } catch (_: ActivityNotFoundException) {
        // Teléfono sin Play Store: no hay forma de instalar Health Connect desde la app.
    }
}

/** Aviso cuando Health Connect no se puede usar. No muestra nada si está disponible. */
@Composable
fun HealthAvailabilityBanner(availability: HealthAvailability, modifier: Modifier = Modifier) {
    val context = LocalContext.current
    when (availability) {
        HealthAvailability.AVAILABLE -> Unit
        HealthAvailability.UPDATE_REQUIRED -> InfoBanner(
            title = "Instala o actualiza Health Connect",
            body = "Veiller lee los datos de tu reloj desde Health Connect. Instálalo desde Play Store y vuelve aquí.",
            tone = BannerTone.Warning,
            modifier = modifier,
        ) {
            Spacer(Modifier.height(12.dp))
            PrimaryButton("Abrir Play Store", onClick = { context.openHealthConnectInStore() })
        }
        HealthAvailability.UNAVAILABLE -> InfoBanner(
            title = "Health Connect no está disponible",
            body = "Este teléfono no es compatible con Health Connect, así que Veiller no puede leer los datos de tu reloj.",
            tone = BannerTone.Warning,
            modifier = modifier,
        )
    }
}

/** Aviso de permisos de Health Connect que faltan, con acceso a concederlos. */
@Composable
fun MissingHealthAccessBanner(
    missing: Set<HealthDataType>,
    onGrant: () -> Unit,
    modifier: Modifier = Modifier,
    title: String = "Conceder acceso a tus datos de salud",
    grantLabel: String = "Conceder acceso",
) {
    val context = LocalContext.current
    InfoBanner(
        title = title,
        body = "Veiller necesita leer desde Health Connect: ${missing.joinToString { it.label.lowercase() }}. " +
            "Si el permiso ya no aparece, actívalo en los ajustes de Health Connect.",
        tone = BannerTone.Warning,
        modifier = modifier,
    ) {
        Spacer(Modifier.height(12.dp))
        PrimaryButton(grantLabel, onClick = onGrant)
        LinkButton("Abrir ajustes de Health Connect", onClick = { context.openHealthConnectSettings() })
    }
}
