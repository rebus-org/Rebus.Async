# Changelog

## 2.0.0-a1

* Test release

## 2.0.0-b01

* Test release

## 2.0.0

* Release 2.0.0

## 3.0.0

* Reduce interval between checking for reply (yields much lower latency)
* Change signature of the async bus extension to allow for optionally passing headers along with the request

## 4.0.0

* Update to Rebus 3

## 5.0.0-b06

* Update to Rebus 4
* Add .NET Core support (netstandard 1.3)
* Update deps to b19
* Change reply correlation method to be reliable by basing it on the new `rbs2-in-reply-to` header
* Dismantle ambient transaction context when blocking the callsite because it does not make sense to enlist request in a transaction