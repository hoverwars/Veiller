package com.veiller.app.data.fake

import com.veiller.app.core.model.Circle
import com.veiller.app.core.model.CircleMember
import com.veiller.app.core.model.JoinRequest
import com.veiller.app.core.model.MemberRole
import com.veiller.app.core.model.PlanTier
import com.veiller.app.core.model.Role
import com.veiller.app.core.model.User

/** Datos de ejemplo tomados de los mockups. */
internal object FakeData {
    const val LAURA_ID = "u-laura"
    const val ROSA_ID = "u-rosa"

    const val ROSA_CIRCLE = "circle-rosa"
    const val CARLOS_CIRCLE = "circle-carlos"
    const val ELENA_CIRCLE = "circle-elena"

    const val ROSA_CODE = "VLR-7K2Q"

    val demoUsers = listOf(
        User(LAURA_ID, "Laura Gómez", "laura@correo.com", Role.CAREGIVER),
        User(ROSA_ID, "Rosa Martínez", "rosa@correo.com", Role.ELDER),
    )

    private val laura = CircleMember(LAURA_ID, "Laura Gómez", MemberRole.ADMIN, relation = "hija", isSponsor = true, phone = "3001234567")
    private val juan = CircleMember("u-juan", "Juan Rodríguez", MemberRole.CAREGIVER, relation = "sobrino", phone = "3109876543")

    val circles = listOf(
        Circle(
            id = ROSA_CIRCLE,
            owner = CircleMember(ROSA_ID, "Rosa Martínez", MemberRole.DATA_OWNER),
            ownerAge = 78,
            plan = PlanTier.FREE,
            members = listOf(CircleMember(ROSA_ID, "Rosa Martínez", MemberRole.DATA_OWNER), laura, juan),
            pendingRequests = listOf(JoinRequest("req-pedro", "Pedro Ruiz")),
        ),
        Circle(
            id = CARLOS_CIRCLE,
            owner = CircleMember("u-carlos", "Carlos Ruiz", MemberRole.DATA_OWNER),
            ownerAge = 81,
            plan = PlanTier.PREMIUM,
            members = listOf(
                CircleMember("u-carlos", "Carlos Ruiz", MemberRole.DATA_OWNER),
                juan.copy(role = MemberRole.ADMIN, relation = null, isSponsor = true),
                CircleMember("u-ana", "Ana Pérez", MemberRole.CAREGIVER),
                laura.copy(role = MemberRole.CAREGIVER, relation = null, isSponsor = false),
            ),
            pendingRequests = emptyList(),
        ),
        Circle(
            id = ELENA_CIRCLE,
            owner = CircleMember("u-elena", "Elena Gómez", MemberRole.DATA_OWNER),
            ownerAge = 69,
            plan = PlanTier.FREE,
            members = listOf(
                CircleMember("u-elena", "Elena Gómez", MemberRole.DATA_OWNER),
                laura.copy(relation = null, isSponsor = true),
            ),
            pendingRequests = emptyList(),
        ),
    )

    /** Signos vitales del momento por círculo (fc, sueño min, spo2, presión, batería, sync min). */
    data class Vitals(
        val heartRate: Int?,
        val sleepMinutes: Int?,
        val spo2: Int?,
        val bloodPressure: String?,
        val battery: Int?,
        val lastSyncMinutes: Int,
    )

    val vitals = mapOf(
        ROSA_CIRCLE to Vitals(128, 310, 96, null, 42, 8),
        CARLOS_CIRCLE to Vitals(72, 425, 98, "120/80", 76, 12),
        ELENA_CIRCLE to Vitals(null, null, null, null, null, 180),
    )
}
