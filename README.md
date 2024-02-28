BinaryQRGenerator is a utility which can generate a QR code from a binary file.
It uses the Binary encoding scheme. This is defined in the QR standard, but seems to be poorly supported by both online generator tools as well as by QR readers.

Please note that for these QR codes to have any meaning, they need to be scanned by an app which has prior knowledge of the binary encoding scheme.

Meaning - if you generate a QR code from a PNG file - this can be scanned by any QR scanner. However, the client application will likely not be able to present this in a way which makes sense to a user. The raw binary data will be displayed.

For the retrieved data to make sense, the client application will also need to handle the incoming data as binary (for example as a file). As the QR code was originally intended to encode text, nummerals, *binary* and Kanji, PNG or MIDI encoding is not generally supported. 

This project includes a MAUI project to read QR codes as well. I've successfully used this for images, but was unsuccessful with MIDI files as of yet.