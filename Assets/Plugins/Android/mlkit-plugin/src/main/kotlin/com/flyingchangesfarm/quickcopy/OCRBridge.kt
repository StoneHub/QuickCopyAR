package com.flyingchangesfarm.quickcopy

import android.content.Context
import android.graphics.Bitmap
import android.graphics.Rect
import android.util.Log
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.text.Text
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.TextRecognizer
import com.google.mlkit.vision.text.latin.TextRecognizerOptions
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.tasks.await
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Bridge class for ML Kit Text Recognition.
 * Called from Unity via AndroidJavaObject.
 */
class OCRBridge private constructor(private val context: Context) {

    private val recognizer: TextRecognizer = TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS)
    private val isModelReady = AtomicBoolean(false)

    companion object {
        private const val TAG = "OCRBridge"

        @Volatile
        private var instance: OCRBridge? = null

        /**
         * Get singleton instance of OCRBridge.
         * Called from Unity: OCRBridge.getInstance(activity)
         */
        @JvmStatic
        fun getInstance(context: Context): OCRBridge {
            return instance ?: synchronized(this) {
                instance ?: OCRBridge(context.applicationContext).also {
                    instance = it
                    Log.d(TAG, "OCRBridge instance created")
                }
            }
        }
    }

    init {
        // Check if model is ready by running a test recognition
        checkModelReady()
    }

    private fun checkModelReady() {
        try {
            // Create a small test image
            val testBitmap = Bitmap.createBitmap(10, 10, Bitmap.Config.ARGB_8888)
            val testImage = InputImage.fromBitmap(testBitmap, 0)

            recognizer.process(testImage)
                .addOnSuccessListener {
                    isModelReady.set(true)
                    Log.d(TAG, "ML Kit model is ready")
                }
                .addOnFailureListener { e ->
                    Log.w(TAG, "ML Kit model may need to download: ${e.message}")
                    // Model will auto-download via Google Play Services
                }

            testBitmap.recycle()
        } catch (e: Exception) {
            Log.e(TAG, "Error checking model readiness: ${e.message}")
        }
    }

    /**
     * Check if the ML Kit model is downloaded and ready.
     */
    fun isModelReady(): Boolean {
        return isModelReady.get()
    }

    /**
     * Process a bitmap and return recognized text.
     * This is a blocking call - runs OCR synchronously.
     *
     * @param bitmap The image to process
     * @return Recognized text or error message prefixed with "ERROR:"
     */
    fun processImage(bitmap: Bitmap): String {
        return runBlocking {
            try {
                Log.d(TAG, "Processing image: ${bitmap.width}x${bitmap.height}")

                val image = InputImage.fromBitmap(bitmap, 0)
                val result = recognizer.process(image).await()

                val text = result.text
                Log.d(TAG, "OCR completed. Text length: ${text.length}")

                if (text.isEmpty()) {
                    ""
                } else {
                    text
                }
            } catch (e: Exception) {
                Log.e(TAG, "OCR processing error: ${e.message}")
                "ERROR: ${e.message}"
            }
        }
    }

    /**
     * Process a bitmap and return detailed results with bounding boxes as JSON.
     *
     * @param bitmap The image to process
     * @return JSON string with text and block information
     */
    fun processImageWithBounds(bitmap: Bitmap): String {
        return runBlocking {
            try {
                Log.d(TAG, "Processing image with bounds: ${bitmap.width}x${bitmap.height}")

                val image = InputImage.fromBitmap(bitmap, 0)
                val result = recognizer.process(image).await()

                val response = JSONObject()
                response.put("text", result.text)

                val blocksArray = JSONArray()
                for (block in result.textBlocks) {
                    val blockJson = JSONObject()
                    blockJson.put("text", block.text)

                    block.boundingBox?.let { rect ->
                        blockJson.put("left", rect.left)
                        blockJson.put("top", rect.top)
                        blockJson.put("width", rect.width())
                        blockJson.put("height", rect.height())
                    }

                    // ML Kit doesn't provide confidence for blocks, use 1.0 as default
                    blockJson.put("confidence", 1.0)

                    // Add lines within the block
                    val linesArray = JSONArray()
                    for (line in block.lines) {
                        val lineJson = JSONObject()
                        lineJson.put("text", line.text)

                        line.boundingBox?.let { rect ->
                            lineJson.put("left", rect.left)
                            lineJson.put("top", rect.top)
                            lineJson.put("width", rect.width())
                            lineJson.put("height", rect.height())
                        }

                        line.confidence?.let {
                            lineJson.put("confidence", it)
                        }

                        linesArray.put(lineJson)
                    }
                    blockJson.put("lines", linesArray)

                    blocksArray.put(blockJson)
                }

                response.put("blocks", blocksArray)
                response.put("blockCount", result.textBlocks.size)

                Log.d(TAG, "OCR completed with bounds. Blocks: ${result.textBlocks.size}")

                response.toString()
            } catch (e: Exception) {
                Log.e(TAG, "OCR processing error: ${e.message}")
                "ERROR: ${e.message}"
            }
        }
    }

    /**
     * Process image asynchronously with callback.
     * Useful for non-blocking operations from Unity.
     *
     * @param bitmap The image to process
     * @param callback Unity callback object name
     * @param methodName Unity callback method name
     */
    fun processImageAsync(bitmap: Bitmap, callback: String, methodName: String) {
        try {
            val image = InputImage.fromBitmap(bitmap, 0)

            recognizer.process(image)
                .addOnSuccessListener { result ->
                    val text = result.text
                    Log.d(TAG, "Async OCR completed. Text length: ${text.length}")

                    // Send result back to Unity
                    com.unity3d.player.UnityPlayer.UnitySendMessage(callback, methodName, text)
                }
                .addOnFailureListener { e ->
                    Log.e(TAG, "Async OCR error: ${e.message}")
                    com.unity3d.player.UnityPlayer.UnitySendMessage(callback, methodName, "ERROR: ${e.message}")
                }
        } catch (e: Exception) {
            Log.e(TAG, "Error starting async OCR: ${e.message}")
            com.unity3d.player.UnityPlayer.UnitySendMessage(callback, methodName, "ERROR: ${e.message}")
        }
    }

    /**
     * Close the recognizer and release resources.
     * Call this when the app is closing.
     */
    fun close() {
        try {
            recognizer.close()
            Log.d(TAG, "OCRBridge closed")
        } catch (e: Exception) {
            Log.e(TAG, "Error closing OCRBridge: ${e.message}")
        }
    }
}
