package com.veiller.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Modifier
import com.veiller.app.core.designsystem.theme.VeillerColors
import com.veiller.app.core.designsystem.theme.VeillerTheme
import com.veiller.app.navigation.VeillerNavHost
import com.veiller.app.navigation.startDestinationFor
import kotlinx.coroutines.flow.first

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val auth = (application as VeillerApplication).container.authRepository
        setContent {
            VeillerTheme {
                // La primera pantalla depende de la sesión guardada en DataStore.
                val start by produceState<Any?>(initialValue = null) {
                    value = startDestinationFor(auth.session.first())
                }
                val destination = start
                if (destination == null) {
                    Box(Modifier.fillMaxSize().background(VeillerColors.Background))
                } else {
                    VeillerNavHost(startDestination = destination)
                }
            }
        }
    }
}
