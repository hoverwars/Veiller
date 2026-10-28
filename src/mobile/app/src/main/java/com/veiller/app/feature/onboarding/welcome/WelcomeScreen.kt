package com.veiller.app.feature.onboarding.welcome

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.RadioButtonDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.semantics.Role as SemanticsRole
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.LinkButton
import com.veiller.app.core.designsystem.component.PrimaryButton
import com.veiller.app.core.designsystem.component.VeillerScreen
import com.veiller.app.core.designsystem.icon.VeillerIcons
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.core.model.Role

/** A1 · Bienvenida y elección de rol. */
@Composable
fun WelcomeScreen(onContinue: (Role) -> Unit, onLogin: () -> Unit) {
    var selected by rememberSaveable { mutableStateOf<Role?>(null) }
    WelcomeContent(selected = selected, onSelect = { selected = it }, onContinue = { selected?.let(onContinue) }, onLogin = onLogin)
}

@Composable
fun WelcomeContent(selected: Role?, onSelect: (Role) -> Unit, onContinue: () -> Unit, onLogin: () -> Unit) {
    VeillerScreen(
        bottomBar = {
            PrimaryButton("Continuar", onClick = onContinue, enabled = selected != null)
            LinkButton("Ya tengo cuenta · Iniciar sesión", onClick = onLogin)
        },
    ) {
        Spacer(Modifier.height(56.dp))
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            VeillerLogo()
            Spacer(Modifier.height(20.dp))
            Text("Veiller", style = MaterialTheme.typography.headlineMedium)
            Spacer(Modifier.height(8.dp))
            Text(
                "Tranquilidad para quien cuida.\nAlertas cuando algo se sale de lo normal.",
                style = MaterialTheme.typography.bodyMedium,
                color = VeillerColors.TextSecondary,
                textAlign = TextAlign.Center,
            )
        }
        Spacer(Modifier.height(48.dp))
        Text("¿Cómo usarás Veiller?", style = MaterialTheme.typography.titleMedium)
        Spacer(Modifier.height(12.dp))
        RoleOption(
            letter = "C",
            title = "Soy cuidador",
            subtitle = "Recibo alertas y reviso el estado de mis pacientes",
            selected = selected == Role.CAREGIVER,
            onClick = { onSelect(Role.CAREGIVER) },
        )
        Spacer(Modifier.height(12.dp))
        RoleOption(
            letter = "A",
            title = "Soy el adulto mayor",
            subtitle = "Uso el smartwatch y comparto mis datos",
            selected = selected == Role.ELDER,
            onClick = { onSelect(Role.ELDER) },
        )
    }
}

@Composable
private fun RoleOption(letter: String, title: String, subtitle: String, selected: Boolean, onClick: () -> Unit) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .selectable(selected = selected, onClick = onClick, role = SemanticsRole.RadioButton),
        shape = MaterialTheme.shapes.large,
        color = if (selected) VeillerColors.PrimaryContainer.copy(alpha = 0.5f) else VeillerColors.Surface,
        border = BorderStroke(if (selected) 1.5.dp else 1.dp, if (selected) VeillerColors.Primary else VeillerColors.Border),
    ) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            InitialsAvatar(letter, size = 36.dp)
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(title, style = MaterialTheme.typography.titleSmall)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
            }
            RadioButton(
                selected = selected,
                onClick = null,
                colors = RadioButtonDefaults.colors(selectedColor = VeillerColors.Primary),
            )
        }
    }
}

@Composable
fun VeillerLogo(size: Dp = 88.dp) {
    Box(
        Modifier
            .size(size)
            .clip(RoundedCornerShape(22.dp))
            .background(VeillerColors.Primary),
        contentAlignment = Alignment.Center,
    ) {
        Canvas(Modifier.fillMaxSize().padding(size * 0.18f)) {
            drawCircle(
                color = Color(0xFF8FC9C9),
                style = Stroke(width = 2.dp.toPx(), pathEffect = PathEffect.dashPathEffect(floatArrayOf(2f, 8f))),
            )
        }
        Icon(VeillerIcons.Pulse, contentDescription = null, tint = Color.White, modifier = Modifier.size(size * 0.36f))
    }
}

@Preview(showBackground = true)
@Composable
private fun WelcomePreview() {
    VeillerTheme { WelcomeContent(selected = Role.CAREGIVER, onSelect = {}, onContinue = {}, onLogin = {}) }
}
