package com.veiller.app.data.fake

import com.veiller.app.core.model.Circle
import com.veiller.app.core.model.DataPoint
import com.veiller.app.core.model.HistoryRange
import com.veiller.app.core.model.PatientHistory
import com.veiller.app.core.model.PatientSummary
import com.veiller.app.data.repository.PatientRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

class FakePatientRepository(private val circles: FakeCircleRepository) : PatientRepository {

    override fun observePatients(): Flow<List<PatientSummary>> =
        circles.circles.map { all -> all.values.map(::toSummary) }

    override suspend fun getPatient(circleId: String): PatientSummary? {
        delay(NETWORK_DELAY_MS)
        return circles.circles.value[circleId]?.let(::toSummary)
    }

    override suspend fun getHistory(circleId: String, range: HistoryRange): PatientHistory {
        delay(NETWORK_DELAY_MS)
        if (FakeData.vitals[circleId]?.heartRate == null) {
            return PatientHistory(range, emptyList(), emptyList())
        }
        return when (range) {
            HistoryRange.DAY -> PatientHistory(
                range,
                restingHeartRate = points(listOf("0h", "3h", "6h", "9h", "12h", "15h", "18h", "21h"), 64f, 70f, 74f, 81f, 79f, 77f, 75f, 70f),
                sleepHours = listOf(DataPoint("Anoche", 5.17f)),
            )
            HistoryRange.WEEK -> PatientHistory(
                range,
                restingHeartRate = points(WeekLabels, 76f, 77f, 75f, 78f, 76f, 79f, 80f),
                sleepHours = points(WeekLabels, 7.2f, 6.8f, 7.5f, 6.9f, 6.8f, 7.1f, 5.2f),
            )
            HistoryRange.MONTH -> PatientHistory(
                range,
                restingHeartRate = (1..30).map { DataPoint(if (it % 5 == 1) "$it" else "", 72f + (it * 7 % 9)) },
                sleepHours = (1..30).map { DataPoint(if (it % 5 == 1) "$it" else "", 6f + (it * 3 % 5) / 4f) },
            )
        }
    }

    private fun toSummary(circle: Circle): PatientSummary {
        val v = FakeData.vitals[circle.id]
        return PatientSummary(
            circleId = circle.id,
            fullName = circle.owner.fullName,
            age = circle.ownerAge,
            plan = circle.plan,
            caregiverCount = circle.caregivers.size,
            heartRateBpm = v?.heartRate,
            sleepMinutes = v?.sleepMinutes,
            spo2Percent = v?.spo2,
            bloodPressure = v?.bloodPressure,
            batteryPercent = v?.battery,
            lastSyncMinutesAgo = v?.lastSyncMinutes ?: 0,
        )
    }

    private fun points(labels: List<String>, vararg values: Float) =
        labels.zip(values.toList()) { label, value -> DataPoint(label, value) }

    private companion object {
        val WeekLabels = listOf("J", "V", "S", "D", "L", "M", "X")
    }
}
