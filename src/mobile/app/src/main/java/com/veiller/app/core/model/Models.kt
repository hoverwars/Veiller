package com.veiller.app.core.model

import java.time.Instant

enum class Role(val label: String) {
    CAREGIVER("Cuidador"),
    ELDER("Adulto mayor"),
}

data class User(
    val id: String,
    val fullName: String,
    val email: String,
    val role: Role,
) {
    val firstName: String get() = fullName.trim().substringBefore(' ')
}

data class Session(
    val user: User,
    val onboardingCompleted: Boolean,
)

enum class PlanTier(val label: String, val maxCaregivers: Int) {
    FREE("Gratis", 2),
    PREMIUM("Premium", 8),
}

enum class MemberRole(val label: String) {
    DATA_OWNER("Titular de los datos"),
    ADMIN("Administrador"),
    CAREGIVER("Cuidador"),
}

data class CircleMember(
    val userId: String,
    val fullName: String,
    val role: MemberRole,
    val relation: String? = null,
    val isSponsor: Boolean = false,
    val phone: String? = null,
) {
    val initials: String get() = initialsOf(fullName)
}

data class JoinRequest(
    val id: String,
    val fullName: String,
) {
    val initials: String get() = initialsOf(fullName)
}

data class Circle(
    val id: String,
    val owner: CircleMember,
    val ownerAge: Int,
    val plan: PlanTier,
    val members: List<CircleMember>,
    val pendingRequests: List<JoinRequest>,
) {
    val caregivers: List<CircleMember> get() = members.filter { it.role != MemberRole.DATA_OWNER }
    val isFull: Boolean get() = caregivers.size >= plan.maxCaregivers
    val admin: CircleMember? get() = members.firstOrNull { it.role == MemberRole.ADMIN }
}

/** Lo que ve un cuidador al ingresar un código antes de pedir unirse. */
data class CirclePreview(
    val code: String,
    val ownerName: String,
    val ownerAge: Int,
    val adminName: String?,
    val plan: PlanTier,
    val caregiverCount: Int,
) {
    val ownerInitials: String get() = initialsOf(ownerName)
}

data class InviteCode(
    val code: String,
    val expiresAt: Instant,
)

data class PatientSummary(
    val circleId: String,
    val fullName: String,
    val age: Int,
    val plan: PlanTier,
    val caregiverCount: Int,
    val heartRateBpm: Int?,
    val sleepMinutes: Int?,
    val spo2Percent: Int?,
    val bloodPressure: String?,
    val batteryPercent: Int?,
    val lastSyncMinutesAgo: Int,
) {
    val initials: String get() = initialsOf(fullName)
}

enum class HistoryRange(val label: String) {
    DAY("Día"),
    WEEK("7 días"),
    MONTH("30 días"),
}

data class DataPoint(val label: String, val value: Float?)

data class PatientHistory(
    val range: HistoryRange,
    val restingHeartRate: List<DataPoint>,
    val sleepHours: List<DataPoint>,
)

enum class HealthDataType(val label: String, val required: Boolean) {
    HEART_RATE("Ritmo cardíaco", true),
    LOCATION("Ubicación", true),
    SLEEP("Sueño", true),
    SCREEN_TIME("Tiempo en pantalla", true),
    BLOOD_PRESSURE("Presión arterial", false),
    SPO2("Oxigenación (SpO₂)", false),
    ECG("Electrocardiograma", false),
}

data class ConsentItem(
    val type: HealthDataType,
    val granted: Boolean,
)

data class WatchStatus(
    val connected: Boolean,
    val batteryPercent: Int?,
)

data class ElderHome(
    val lastSendMinutesAgo: Int,
    val watch: WatchStatus,
    val caregivers: List<CircleMember>,
)

fun initialsOf(name: String): String =
    name.trim().split(Regex("\\s+")).filter { it.isNotEmpty() }.take(2)
        .joinToString("") { it.first().uppercase() }
