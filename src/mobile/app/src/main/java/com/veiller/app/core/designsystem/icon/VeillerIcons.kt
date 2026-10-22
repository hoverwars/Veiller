package com.veiller.app.core.designsystem.icon

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.addPathNodes
import androidx.compose.ui.unit.dp

/** Íconos propios para lo que no trae material-icons-core. */
object VeillerIcons {

    val Circle: ImageVector by lazy {
        icon("Circle") {
            stroked("M5,12a7,7 0 1,1 14,0a7,7 0 1,1 -14,0")
            filled("M9.5,12a2.5,2.5 0 1,1 5,0a2.5,2.5 0 1,1 -5,0")
        }
    }

    val Moon: ImageVector by lazy {
        icon("Moon") { filled("M12,3a9,9 0 1,0 9,9a7,7 0 0,1 -9,-9z") }
    }

    val Watch: ImageVector by lazy {
        icon("Watch") {
            filled("M9,2h6l1,4H8z")
            filled("M8,18h8l-1,4H9z")
            filled("M8,6h8a2,2 0 0,1 2,2v8a2,2 0 0,1 -2,2H8a2,2 0 0,1 -2,-2V8a2,2 0 0,1 2,-2z")
        }
    }

    val Battery: ImageVector by lazy {
        icon("Battery") {
            filled("M9,2h6v2h2a1,1 0 0,1 1,1v16a1,1 0 0,1 -1,1H7a1,1 0 0,1 -1,-1V5a1,1 0 0,1 1,-1h2z")
        }
    }

    val Pulse: ImageVector by lazy {
        icon("Pulse") { stroked("M3,12h4l2,-5l4,10l2,-5h6") }
    }

    private class Builder(val inner: ImageVector.Builder) {
        fun filled(path: String) {
            inner.addPath(pathData = addPathNodes(path), fill = SolidColor(Color.Black))
        }

        fun stroked(path: String) {
            inner.addPath(
                pathData = addPathNodes(path),
                stroke = SolidColor(Color.Black),
                strokeLineWidth = 2f,
                strokeLineCap = StrokeCap.Round,
                strokeLineJoin = StrokeJoin.Round,
            )
        }
    }

    private fun icon(name: String, block: Builder.() -> Unit): ImageVector {
        val builder = ImageVector.Builder(
            name = name,
            defaultWidth = 24.dp,
            defaultHeight = 24.dp,
            viewportWidth = 24f,
            viewportHeight = 24f,
        )
        Builder(builder).block()
        return builder.build()
    }
}
