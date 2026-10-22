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
import java.time.Duration

/** Crea un ViewModel con acceso al AppContainer y a los argumentos de navegación. */
@Composable
inline fun <reified VM : ViewModel> veillerViewModel(
    crossinline create: (container: AppContainer, handle: SavedStateHandle) -> VM,
): VM {
    val app = LocalContext.current.applicationContext as VeillerApplication
    return viewModel(factory = viewModelFactory { initializer { create(app.container, createSavedStateHandle()) } })
}

object Format {
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
