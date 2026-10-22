package com.veiller.app

import android.app.Application
import com.veiller.app.di.AppContainer

class VeillerApplication : Application() {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
    }
}
