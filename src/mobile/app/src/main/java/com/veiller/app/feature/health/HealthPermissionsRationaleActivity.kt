package com.veiller.app.feature.health

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme

/**
 * Justificación de permisos que abre Health Connect: desde su diálogo (Android 13 o menos,
 * ACTION_SHOW_PERMISSIONS_RATIONALE) y desde "Permisos de apps" (Android 14+, alias
 * ViewPermissionUsageActivity del manifest).
 */
class HealthPermissionsRationaleActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            VeillerTheme {
                VeillerScreen(title = "Tus datos de salud en Veiller", onBack = ::finish) {
                    RationaleParagraph(
                        "Qué leemos",
                        "Tu ritmo cardíaco, tus pasos y tu sueño, desde Health Connect. " +
                            "Los escribe la app de tu reloj (por ejemplo, Mi Fitness).",
                    )
                    RationaleParagraph(
                        "Para qué",
                        "Para mostrártelos y compartirlos solo con los cuidadores que tú apruebes en tu círculo. " +
                            "Para eso se enviarán a los servidores de Veiller.",
                    )
                    RationaleParagraph(
                        "Cómo retirarlo",
                        "Cuando quieras, desde \"Mis permisos\" en Veiller o desde Ajustes > Health Connect > " +
                            "Permisos de apps > Veiller.",
                    )
                    RationaleParagraph(
                        "Tus derechos",
                        "Son datos sensibles según la Ley 1581 de 2012: puedes conocerlos, actualizarlos y " +
                            "pedir que se eliminen.",
                    )
                }
            }
        }
    }
}

@Composable
private fun RationaleParagraph(title: String, body: String) {
    Text(title, style = MaterialTheme.typography.titleSmall)
    Spacer(Modifier.height(4.dp))
    Text(body, style = MaterialTheme.typography.bodyMedium, color = VeillerColors.TextSecondary)
    Spacer(Modifier.height(16.dp))
}
