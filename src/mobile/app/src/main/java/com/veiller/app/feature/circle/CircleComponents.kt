package com.veiller.app.feature.circle

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.veiller.app.core.designsystem.component.BannerTone
import com.veiller.app.core.designsystem.component.InfoBanner
import com.veiller.app.core.designsystem.component.InitialsAvatar
import com.veiller.app.core.designsystem.component.LinkButton
import com.veiller.app.core.designsystem.component.VeillerCard
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.model.CircleMember
import com.veiller.app.core.model.JoinRequest
import com.veiller.app.core.model.PlanTier

/** "Laura Gómez (hija)" */
fun CircleMember.displayName(isCurrentUser: Boolean = false): String = buildString {
    append(fullName)
    when {
        isCurrentUser -> append(" (tú)")
        relation != null -> append(" ($relation)")
    }
}

/** "Administrador · Sponsor" */
fun CircleMember.roleLine(): String = if (isSponsor) "${role.label} · Sponsor" else role.label

fun PlanTier.planName(): String = if (this == PlanTier.FREE) "Plan gratuito" else "Plan Premium"

@Composable
fun MemberRow(
    member: CircleMember,
    modifier: Modifier = Modifier,
    isCurrentUser: Boolean = false,
    trailing: (@Composable () -> Unit)? = null,
) {
    VeillerCard(modifier = modifier, contentPadding = PaddingValues(horizontal = 14.dp, vertical = 12.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            InitialsAvatar(member.initials)
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(member.displayName(isCurrentUser), style = MaterialTheme.typography.titleSmall)
                Text(member.roleLine(), style = MaterialTheme.typography.bodySmall, color = VeillerColors.TextSecondary)
            }
            trailing?.invoke()
        }
    }
}

/** Solicitud pendiente con acciones Aprobar / Rechazar. */
@Composable
fun JoinRequestCard(
    request: JoinRequest,
    canApprove: Boolean,
    onApprove: () -> Unit,
    onReject: () -> Unit,
    modifier: Modifier = Modifier,
    blockedReason: String? = null,
) {
    InfoBanner(
        title = "${request.fullName} quiere unirse",
        body = blockedReason,
        tone = if (blockedReason != null) BannerTone.Warning else BannerTone.Teal,
        modifier = modifier,
    ) {
        if (canApprove) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                if (blockedReason == null) {
                    LinkButton("Aprobar", onClick = onApprove)
                    Text("·", color = VeillerColors.TextSecondary)
                }
                LinkButton("Rechazar", onClick = onReject)
            }
        }
    }
}
