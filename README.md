# Oasis
Oasis Automation Services

Oasis is a distributed services architecture for applications like home automation and perhaps robotics.

## Goals
  * A distributed service architecture that uses one code model for services across all target platforms and OS's
  * Services that are distributed, composable and inspectable
  * A model for defining static and dynamic service relationships
  * Concrete hardware and services for display endpoints, control endpoints, sensing endpoints, actuators

## Status

Nothing works. Extremely early days here. Mostly just working out core protocol and service design elements.

## Example services

1. Display Endpoints
  * Magic mirror - a half-silvered mirror with an embedded monitor. Recognizes you and displays personalized information to you based on multiple dimensions of personalization
  * HDMI pass-through device, augmenting HDMI inputs with additional annotations, notifications, control surfaces, etc.
  * Notification lights and devices
  * Streaming speakers and audio inputs to media center
1. Sensor Endpoints
  * Temp and humidity sensors, indoor and outdoor
  * Weather forecast retrieval
  * Calendar observer
  * Location detection services (where are you and where are you likely going? Should I warm up the house?)
1. Control Endpoints
  * TV remote
  * Thermostat
  * Mobile devices (iOS, Android, Windows)
  * Wearables (existing Android Wear and new custom NETMF devices)
  * 3D printer front panel
  * Microphones for speech control
  * Cameras for facial recognition and personalization
  * Wall-mounted control panels
  * Smart wall switches with software-defined functions
1. Actuator Endpoints
  * Environmental controls (temp, humidity, lighting)
  * Media controls
  * Automotive controls
  * 3D printer print manager
  * Electronic locks
  * Garage door opener
1. Miscelaneous
  * Infrastructure services (discovery, security and peer management)
  * Media manager
  * Local cloud (local controller for coordinating LAN access to services)
  * Cloud services (for managing out-of-the-home/WAN access to services)
  * Proxy services (for managing IoT devices via bluetooth/Xbee where those devices do not have a TCP stack or cannot host services)

## Other nifty design elements

### Knock to pair
Pairing security is difficult to manage at best. It ends up either being insecure or baffling to the end user (or both). I intend to use
a knock-to-pair mechanism where you tap a switch or physically knock in a rhythmic pattern on the local controller or perhaps any already paired device, and then tap/knock that
same pattern into the unpaired device. If the patterns match, a key exchange will take place to bring the new device into the network.
A similar knock-to-unpair protocol might be employed to add a level of theft deterrence. This still needs refinement, but this is my starting point for pairing.

## Wait, don't I know you?
Maybe. I was an architect and developer on the original Microsoft Robotics Development Studio suite and I am still a
Microsoft employee working on unrelated projects now. You may recognize that some of the
goals listed above (composable and inspectable services, for instance) are similar to what RDS was trying to achieve. This project
may borrow philosophically from RDS, but does not use any code from RDS nor to my knowlege any proprietary information.
This is a completely new open-source implementation with significantly different goals. Notably, this
implementation is not at all robotics-specific and has some major differences in core design decisions.
My main interest now is in home automation and lifestyle automation in a truly convenient an inobtrustive fashion. Automation that I could live with.

## Why C# ?
The choice of C# for me was an easy one. I have deep experience with C# and more importantly, with Mono and Xamarin, it runs everywhere
that I might care to have my Oasis services running.  That includes:
1. All modern versions and flavors of Windows 
  * Windows 7 and 8 desktop, server, and immersive apps
  * Window 8 phone (WPA profile)
  * Windows 10 desktop, server, immersive and phone (universal apps)
1. IoT-scale devices like 
  * Raspberry Pi running Linux (Mono)
  * Raspberry Pi 2 running Windows 10
  * and even STM32 chips via .Net Micro Framework for things like wearable devices
1. All modern versions of Linux supporting Mono
1. Mobile devices via Xamarin, including:
  * iOS
  * Android
  * Android Wear
  * pretty much anything else that Xamarin supports
1. Cloud services

This is a broader reach than I can achieve with java and although I could achieve the same reach with C/C++ it would require significantly more effort in the form of per-platform adaptations. 
My task is made a lot simpler because I know I have a well-defined set of class libraries on every target platform and that those libraries take care of most of the platform adaptations I care about. 
