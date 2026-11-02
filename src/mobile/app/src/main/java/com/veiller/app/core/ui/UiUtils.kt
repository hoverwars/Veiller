package com.veiller.app.core.ui

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import androidx.core.net.toUri
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.createSavedStateHandle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import com.veiller.app.VeillerApplication
import com.veiller.app.di.AppContainer
import java.text.NumberFormat
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** Crea un ViewModel con acceso al AppContainer y a los argumentos de navegación. */
@Composable
inline fun <reified VM : ViewModel> veillerViewModel(
    crossinline create: (container: AppContainer, handle: SavedStateHandle) -> VM,
): VM {
    val app = LocalContext.current.applicationContext as VeillerApplication
    return viewModel(factory = viewModelFactory { initializer { create(app.container, createSavedStateHandle()) } })
}

object Format {
    private val AppLocale: Locale = Locale.forLanguageTag("es-CO")
    private val ClockFormat = DateTimeFormatter.ofPattern("HH:mm", AppLocale)
    private val DayFormat = DateTimeFormatter.ofPattern("dd/MM", AppLocale)

    /** 310 -> "5 h 10" */
    fun sleep(minutes: Int?): String =
        minutes?.let { "${it / 60} h ${(it % 60).toString().padStart(2, '0')}" } ?: "—"

    /** 310 -> "5:10" */
    fun sleepClock(minutes: Int?): String =
        minutes?.let { "${it / 60}:${(it % 60).toString().padStart(2, '0')}" } ?: "—"

    /** 6.83 -> "6 h 50" */
    fun hours(value: Float): String = sleep((value * 60).toInt())

    /** 8 -> "8 min", 180 -> "3 h" */
    fun ago(minutes: Int): String = when {
        minutes < 60 -> "$minutes min"
        minutes < 60 * 24 -> "${minutes / 60} h"
        else -> "${minutes / (60 * 24)} d"
    }

    /** Antigüedad de una medición: "hace un momento", "hace 8 min", "hace 8 h". */
    fun agoSince(at: Instant, now: Instant): String {
        val minutes = Duration.between(at, now).toMinutes()
        return if (minutes < 1) "hace un momento" else "hace ${ago(minutes.toInt())}"
    }

    /** Hora real de una medición: "08:57" si es de hoy, "ayer 23:10" o "30/10 23:10". */
    fun clockTime(at: Instant, now: Instant, zone: ZoneId): String {
        val time = at.atZone(zone)
        val today = now.atZone(zone).toLocalDate()
        val clock = time.format(ClockFormat)
        return when (time.toLocalDate()) {
            today -> clock
            today.minusDays(1) -> "ayer $clock"
            else -> "${time.format(DayFormat)} $clock"
        }
    }

    /** "08:57 · hace 8 h" */
    fun measuredAt(at: Instant, now: Instant, zone: ZoneId): String = "${clockTime(at, now, zone)} · ${agoSince(at, now)}"

    /** 8894 -> "8.894" */
    fun count(value: Long): String = NumberFormat.getIntegerInstance(AppLocale).format(value)

    fun caregivers(count: Int): String = if (count == 1) "1 cuidador" else "$count cuidadores"

    fun remaining(duration: Duration): String {
        if (duration.isNegative || duration.isZero) return "Código vencido"
        return "Vence en ${duration.toHours()} h ${duration.toMinutesPart()} min"
    }
}

fun Context.dial(phone: String) {
    startActivity(Intent(Intent.ACTION_DIAL, "tel:$phone".toUri()))
}

fun Context.shareText(text: String) {
    val send = Intent(Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(Intent.EXTRA_TEXT, text)
    }
    try {
        startActivity(Intent.createChooser(send, null))
    } catch (_: ActivityNotFoundException) {
        // Sin apps para compartir: no hay nada más que hacer.
    }
}
