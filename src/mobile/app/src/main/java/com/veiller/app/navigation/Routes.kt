package com.veiller.app.navigation

import kotlinx.serialization.Serializable

// Onboarding
@Serializable object WelcomeRoute
@Serializable data class RegisterRoute(val role: String)
@Serializable object LoginRoute
@Serializable data class ConsentRoute(val mode: String, val circleId: String? = null)
@Serializable object InviteRoute
@Serializable data class JoinCircleRoute(val fromOnboarding: Boolean)

// Cuidador
@Serializable object CaregiverHomeRoute
@Serializable data class PatientDetailRoute(val circleId: String)
@Serializable data class HistoryRoute(val circleId: String)
@Serializable object AlertsRoute
@Serializable object CirclesRoute
@Serializable data class CircleDetailRoute(val circleId: String)
@Serializable object ProfileRoute

// Adulto mayor
@Serializable object ElderHomeRoute

// Pantallas aún no implementadas (Premium, reglas de alerta...)
@Serializable data class ComingSoonRoute(val title: String)
