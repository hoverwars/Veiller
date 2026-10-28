package com.veiller.app.navigation

import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.consumeWindowInsets
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.navigation.NavController
import androidx.navigation.NavDestination
import androidx.navigation.NavDestination.Companion.hasRoute
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.toRoute
import com.veiller.app.core.designsystem.icon.VeillerIcons
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.model.Role
import com.veiller.app.core.model.Session
import com.veiller.app.feature.auth.LoginScreen
import com.veiller.app.feature.auth.RegisterScreen
import com.veiller.app.feature.caregiver.history.HistoryScreen
import com.veiller.app.feature.caregiver.home.CaregiverHomeScreen
import com.veiller.app.feature.caregiver.patient.PatientDetailScreen
import com.veiller.app.feature.circle.detail.CircleScreen
import com.veiller.app.feature.circle.detail.CirclesScreen
import com.veiller.app.feature.circle.join.JoinCircleScreen
import com.veiller.app.feature.elder.ElderHomeScreen
import com.veiller.app.feature.onboarding.consent.ConsentMode
import com.veiller.app.feature.onboarding.consent.ConsentScreen
import com.veiller.app.feature.onboarding.invite.InviteScreen
import com.veiller.app.feature.onboarding.welcome.WelcomeScreen
import com.veiller.app.feature.profile.ComingSoonScreen
import com.veiller.app.feature.profile.ProfileScreen
import kotlin.reflect.KClass

/** Primera pantalla según la sesión guardada. */
fun startDestinationFor(session: Session?): Any = when {
    session == null -> WelcomeRoute
    !session.onboardingCompleted && session.user.role == Role.CAREGIVER -> JoinCircleRoute(fromOnboarding = true)
    !session.onboardingCompleted -> ConsentRoute(ConsentMode.ONBOARDING.name)
    else -> homeFor(session.user.role)
}

private fun homeFor(role: Role): Any = if (role == Role.CAREGIVER) CaregiverHomeRoute else ElderHomeRoute

/** Navega a [route] dejándola como única pantalla del back stack. */
private fun NavController.resetTo(route: Any) = navigate(route) {
    popUpTo(graph.id) { inclusive = true }
}

private fun NavController.openTab(tab: CaregiverTab) = navigate(tab.route) {
    popUpTo(CaregiverHomeRoute) { saveState = true }
    launchSingleTop = true
    restoreState = true
}

/** Flecha de volver solo si hay a dónde volver. */
private fun NavHostController.backOrNull(): (() -> Unit)? =
    if (previousBackStackEntry != null) { { popBackStack() } } else null

private fun NavController.comingSoon(title: String) = navigate(ComingSoonRoute(title))

@Composable
fun VeillerNavHost(startDestination: Any) {
    val nav = rememberNavController()
    val backStackEntry by nav.currentBackStackEntryAsState()
    val currentTab = backStackEntry?.destination?.let(CaregiverTab::of)

    Scaffold(
        containerColor = VeillerColors.Background,
        contentWindowInsets = WindowInsets(0),
        bottomBar = {
            if (currentTab != null) VeillerBottomBar(currentTab, onSelect = nav::openTab)
        },
    ) { padding ->
        NavHost(
            navController = nav,
            startDestination = startDestination,
            modifier = Modifier.padding(padding).consumeWindowInsets(padding),
        ) {
            // ---------- Onboarding ----------
            composable<WelcomeRoute> {
                WelcomeScreen(
                    onContinue = { role -> nav.navigate(RegisterRoute(role.name)) },
                    onLogin = { nav.navigate(LoginRoute) },
                )
            }
            composable<RegisterRoute> { entry ->
                RegisterScreen(
                    role = Role.valueOf(entry.toRoute<RegisterRoute>().role),
                    onBack = { nav.popBackStack() },
                    onRegistered = { role ->
                        nav.resetTo(
                            if (role == Role.CAREGIVER) JoinCircleRoute(fromOnboarding = true)
                            else ConsentRoute(ConsentMode.ONBOARDING.name),
                        )
                    },
                )
            }
            composable<LoginRoute> {
                LoginScreen(onBack = { nav.popBackStack() }, onLoggedIn = { nav.resetTo(homeFor(it)) })
            }
            composable<ConsentRoute> { entry ->
                val mode = ConsentMode.valueOf(entry.toRoute<ConsentRoute>().mode)
                ConsentScreen(
                    onBack = nav.backOrNull(),
                    onDone = { if (mode == ConsentMode.ONBOARDING) nav.navigate(InviteRoute) else nav.popBackStack() },
                )
            }
            composable<InviteRoute> {
                InviteScreen(onBack = nav.backOrNull(), onFinished = { nav.resetTo(ElderHomeRoute) })
            }
            composable<JoinCircleRoute> {
                JoinCircleScreen(
                    onBack = nav.backOrNull(),
                    onDone = { fromOnboarding -> if (fromOnboarding) nav.resetTo(CaregiverHomeRoute) else nav.popBackStack() },
                )
            }

            // ---------- Cuidador ----------
            composable<CaregiverHomeRoute> {
                CaregiverHomeScreen(
                    onOpenPatient = { nav.navigate(PatientDetailRoute(it)) },
                    onJoinCircle = { nav.navigate(JoinCircleRoute(fromOnboarding = false)) },
                    onOpenAlerts = { nav.openTab(CaregiverTab.Alerts) },
                )
            }
            composable<PatientDetailRoute> {
                PatientDetailScreen(
                    onBack = { nav.popBackStack() },
                    onOpenHistory = { nav.navigate(HistoryRoute(it)) },
                    onOpenCircle = { nav.navigate(CircleDetailRoute(it)) },
                )
            }
            composable<HistoryRoute> {
                HistoryScreen(onBack = { nav.popBackStack() }, onOpenPremium = { nav.comingSoon("Veiller Premium") })
            }
            composable<AlertsRoute> { ComingSoonScreen("Alertas", onBack = null) }
            composable<CirclesRoute> { CirclesScreen(onOpenCircle = { nav.navigate(CircleDetailRoute(it)) }) }
            composable<CircleDetailRoute> {
                CircleScreen(
                    onBack = { nav.popBackStack() },
                    onUpgrade = { nav.comingSoon("Veiller Premium") },
                    onOpenAlertRules = { nav.comingSoon("Reglas de alerta") },
                    onOpenConsents = { nav.navigate(ConsentRoute(ConsentMode.READ_ONLY.name, it)) },
                )
            }
            composable<ProfileRoute> { ProfileScreen(onLoggedOut = { nav.resetTo(WelcomeRoute) }) }

            // ---------- Adulto mayor ----------
            composable<ElderHomeRoute> {
                ElderHomeScreen(
                    onOpenPermissions = { nav.navigate(ConsentRoute(ConsentMode.EDIT.name)) },
                    onLoggedOut = { nav.resetTo(WelcomeRoute) },
                )
            }

            composable<ComingSoonRoute> { entry ->
                ComingSoonScreen(entry.toRoute<ComingSoonRoute>().title, onBack = { nav.popBackStack() })
            }
        }
    }
}

enum class CaregiverTab(val label: String, val icon: ImageVector, val route: Any, val destinations: List<KClass<*>>) {
    Home("Inicio", Icons.Outlined.Home, CaregiverHomeRoute, listOf(CaregiverHomeRoute::class, PatientDetailRoute::class)),
    Alerts("Alertas", Icons.Outlined.Notifications, AlertsRoute, listOf(AlertsRoute::class)),
    Circle("Círculo", VeillerIcons.Circle, CirclesRoute, listOf(CirclesRoute::class, CircleDetailRoute::class)),
    Profile("Perfil", Icons.Outlined.Person, ProfileRoute, listOf(ProfileRoute::class));

    companion object {
        fun of(destination: NavDestination): CaregiverTab? =
            entries.firstOrNull { tab -> tab.destinations.any { destination.hasRoute(it) } }
    }
}

@Composable
private fun VeillerBottomBar(current: CaregiverTab, onSelect: (CaregiverTab) -> Unit) {
    NavigationBar(containerColor = VeillerColors.Surface) {
        CaregiverTab.entries.forEach { tab ->
            NavigationBarItem(
                selected = tab == current,
                onClick = { onSelect(tab) },
                icon = { Icon(tab.icon, contentDescription = null) },
                label = { Text(tab.label) },
                colors = NavigationBarItemDefaults.colors(
                    selectedIconColor = VeillerColors.Primary,
                    selectedTextColor = VeillerColors.Primary,
                    indicatorColor = VeillerColors.PrimaryContainer,
                    unselectedIconColor = VeillerColors.TextSecondary,
                    unselectedTextColor = VeillerColors.TextSecondary,
                ),
            )
        }
    }
}
