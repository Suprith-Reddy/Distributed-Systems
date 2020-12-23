//package com.suprithgurudu

import scala.io.StdIn.readLine

// Encryption Algorithm
object DiffieHellman {
  /**
   * Utility function for computing powers
   * @param base
   * @param expo
   * @return base ** expo
   */
  def power(base: Long, expo: Long): Long = {
    var power_result: Long = 1
    var i: Long = 0
    while(i < expo){
      power_result = power_result * base;
      i += 1
    }
    power_result
  }

  /**
   * Generates key using power function
   * @param G
   * @param m
   * @param P
   * @return key
   */
  def generateKey(G: Long, m: Long, P: Long): Long = {
    if(m == 1)
      return G
    val key: Long = power(G, m) % P
    key
  }

  /**
   * Encrypts the message with the given key
   * @param key
   * @param message
   * @return Encrypted message
   */
  def encryption(key: Long, message: String): String = {
    val len = message.length()
    val adder = key * len
    var encrypted = ""
    for(i <- 0 until len){
      val x = message.charAt(i).toInt-97
      val y = x + adder
      val z = y % 26
      encrypted += (z+97).toChar
    }
    encrypted
  }

  /**
   * Decrypts the message with the given key
   * @param key
   * @param encrypted_message
   * @return decrypted message
   */
  def decryption(key: Long, encrypted_message: String): String = {
    val len = encrypted_message.length()
    val sub = key * len
    var decrypted = ""
    for(i <- 0 until len){
      val x = encrypted_message.charAt(i).toInt-97
      val y = x - sub
      var z = y % 26
      if(z < 0)
        z = z + 26
      decrypted += (z + 97).toChar
    }
    decrypted
  }

  def main(args: Array[String]): Unit = {
    // Read two public keys P & G
    val public_key_G: Long = readLine("Enter first public key (G) [Any Prime Number]: ").toLong
    val public_key_P: Long = readLine("Enter second public key (P) [Any Prime Number]: ").toLong

    // Read private keys
    val alice_private_key: Long = readLine("Enter Alice's private key: ").toLong
    val bob_private_key: Long = readLine("Enter Bob's private key: ").toLong

    // Generate keys with private and public keys
    val alice_key_generated = generateKey(public_key_G, alice_private_key, public_key_P)
    val bob_key_generated = generateKey(public_key_G, bob_private_key, public_key_P)

    // Exchange the generated keys among each other
    val alice_key_received = bob_key_generated
    val bob_key_received = alice_key_generated

    // Generate key with private and exchanged or shared keys
    val alice_secret_key = generateKey(alice_key_received, alice_private_key, public_key_P)
    println("Alice's exchanged secret key to perform symmetric encryption: "+ alice_secret_key)
    val bob_secret_key = generateKey(bob_key_received, bob_private_key, public_key_P)
    println("Bob's exchanged secret key to perform symmetric encryption: "+ bob_secret_key)

    if(alice_secret_key == bob_secret_key)
      println("Both keys shared among Alice and Bob are same!!")
    else
      println("Error: Please enter the valid key values!!")

    // Reads the message to be sent from alice to bob
    val message = readLine("Enter Alice's message to send to Bob [a-z characters with no spaces]: ")
    // Encrypts the message by Alice before sending
    val encrypted = encryption(alice_secret_key, message)
    println("Encrypted message sent to Bob: [" + encrypted + "]")
    // Decrypts the message by Bob after receipt
    val decrypted = decryption(bob_secret_key, encrypted)
    println("Decrypted message by Bob: [" + decrypted + "]")
  }
}
