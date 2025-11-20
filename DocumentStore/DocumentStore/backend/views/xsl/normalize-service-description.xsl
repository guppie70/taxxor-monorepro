<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <!-- 
    Normalizes the different variations of the service descriptions for usage in the Taxxor eco-system
    -->
    <xsl:output method="xml" indent="yes" omit-xml-declaration="no"/>

    <xsl:template match="@* | node()">
        <xsl:copy>
            <xsl:apply-templates select="@* | node()"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="/application">
        <application>
            <xsl:copy-of select="@*"/>
            
            <xsl:choose>
                <xsl:when test="/application/name">
                    <meta>
                        <xsl:apply-templates select="name"/>
                    </meta>
                    <routes>
                        <xsl:apply-templates select="route"/>
                    </routes>
                </xsl:when>
                <xsl:otherwise>
                    <!-- completely copy the whole document -->
                    <xsl:apply-templates/>
                </xsl:otherwise>

            </xsl:choose>

        </application>
    </xsl:template>


</xsl:stylesheet>
