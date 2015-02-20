# Molarity Automation Services

Molarity is a distributed services architecture for home/lifestyle automation.
Molarity works on a wide variety of devices from IoT through cloud, including

  * NETMF devices
  * Raspberry Pi (all versions)
  * Windows PCs, tablets and phones
  * iOS devices
  * Android devices
  * Cloud services (including the Molarity Proxy Service)

## The Name
Well, that will become clear down the road a bit. Suffice to say that Molarity refers to the relative concentration of various molecules in any compound or solution
but not all of the Molecules are public yet.

## Goals
  * A distributed service architecture that uses one code base for services across all target platforms and OS's
  * Services that are distributed, composable and inspectable and inherently fault-tolerant
  * A model for service discovery, description and interaction
  * A model for defining static and dynamic service relationships
  * Concrete hardware and services for display endpoints, control endpoints, sensing endpoints, actuator endpoints, and service endpoints

## Status

Nothing works. Really. There is nothing useful here yet.
It is extremely early days. I am mostly just working out infrastructure and platform abstraction issues and designing the core protocol and service elements. 
It may never get past that stage, because I do have a day job and a short attention span.

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
  * Presence and attention detection (who is where and what are they paying attention to?)
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
  * SIP communication bridge and endpoint communication services
  * Local cloud (local controller for coordinating LAN access to services)
  * Cloud services (for facilitating out-of-the-home/WAN access to services)
  * Proxy services (for managing IoT devices via bluetooth/Xbee where those devices do not have a TCP stack or cannot host services)

## Other design challenges that I want to tackle

### Attention modeling
Say you received an email or text message. Or, say your house is on fire. What is the best way to notify you given the characteristics of
the message, your current activity, and the devices that Molarity could possibly use to notify you? Should the notification even be shown now, or can it wait until the
end of the movie you are watching? And where do we draw the line between automatically learned behaviors and user-set preferences?

### Knock to pair
Pairing security is difficult to manage at best. It ends up either being insecure or baffling to the end user (or both). I intend to use
a knock-to-pair mechanism where you tap a switch or physically knock in a rhythmic pattern on the local controller or perhaps any already paired device, and then tap/knock that
same pattern into the unpaired device. If the patterns match, a key exchange will take place to bring the new device into the network.
A similar knock-to-unpair protocol might be employed to add a level of theft deterrence. This still needs refinement, but this is my starting point for pairing.

### Pervasiveness
Molarity should be in the home in audio, video, haptics, etc. Molarity should be in the car. It should be on my computer, phone, tablet, and wearables wherever I am. But all
good infrastructure is invisible, and Molarity should be invisible too, except when I need it.

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
that I might care to have my Molarity services running.  That includes:

1. All modern versions and flavors of Windows 
  * Windows 7 and 8 desktop, server, and immersive apps
  * Window 8 phone (WPA profile)
  * Windows 10 desktop, server, immersive and phone (universal apps)
1. All modern versions of OSX supporting Mono
1. All modern versions of Linux supporting Mono
1. IoT-scale devices like 
  * Raspberry Pi running Linux (Mono)
  * Raspberry Pi 2 running Windows 10
  * and even SOC systems via .Net Micro Framework for things like wearable devices
1. Mobile devices via Xamarin, including:
  * iOS
  * Android
  * Android Wear
  * pretty much anything else that Xamarin supports
1. Cloud services on all major providers

This is a broader reach than I can achieve with java and although I could achieve the same reach with C/C++ it would require significantly more effort in the form of per-platform adaptations. 
My task is made a lot simpler because I know I have a well-defined set of class libraries on every target platform and that those libraries take care of most of the platform adaptations I care about. 
