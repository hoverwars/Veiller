package com.veiller.app.data.fake

import com.veiller.app.core.model.ConsentItem
import com.veiller.app.core.model.ElderHome
import com.veiller.app.core.model.HealthDataType
import com.veiller.app.core.model.WatchStatus
import com.veiller.app.data.repository.ConsentRepository
import com.veiller.app.data.repository.ElderRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first

class FakeConsentRepository : ConsentRepository {

    private var consents = HealthDataType.entries.map { ConsentItem(it, granted = it != HealthDataType.ECG) }

    override suspend fun getConsents(circleId: String?): List<ConsentItem> {
        delay(NETWORK_DELAY_MS)
        return consents
    }

    override suspend fun saveConsents(items: List<ConsentItem>) {
        delay(NETWORK_DELAY_MS)
        consents = items
    }
}

class FakeElderRepository(private val circles: FakeCircleRepository) : ElderRepository {

    override suspend fun getHome(): ElderHome {
        delay(NETWORK_DELAY_MS)
        val circle = circles.observeCircle(circles.ownCircleId).first()
        return ElderHome(
            lastSendMinutesAgo = 8,
            watch = WatchStatus(connected = true, batteryPercent = 42),
            caregivers = circle?.caregivers.orEmpty(),
        )
    }
}
